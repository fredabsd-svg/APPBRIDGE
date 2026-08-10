using AppBridge.ControlPlane.Domain.Identity;
using AppBridge.ControlPlane.Domain.Tenancy;
using AppBridge.ControlPlane.Domain.Trail;
using AppBridge.ControlPlane.Infrastructure.Auditing;
using AppBridge.ControlPlane.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AppBridge.ControlPlane.Infrastructure.Tests;

/// <summary>
/// T-205 acceptance criterion: "Falha simulada de gravação nega a operação" (V-05, ADR-0007 Part
/// 1). <see cref="ThrowingSaveChangesInterceptor"/> simulates the database itself failing to write
/// — the one failure mode ADR-0007 is actually about (the authorization read that precedes it
/// already fails the whole request if PostgreSQL is down; this is the narrower "reads but doesn't
/// write" window the ADR closes). Real PostgreSQL for everything else, same approach as the rest
/// of this project (no Docker/Testcontainers — docs/SETUP-DEV.md).
/// </summary>
public sealed class AuditWriterTests : IAsyncLifetime
{
    private string _connectionString = null!;
    private Guid _tenantId;
    private Guid _userAccountId;

    public async Task InitializeAsync()
    {
        _connectionString = Environment.GetEnvironmentVariable("APPBRIDGE_TEST_DB_CONNECTION")
            ?? throw new InvalidOperationException(
                "Set APPBRIDGE_TEST_DB_CONNECTION before running the Infrastructure tests (see docs/SETUP-DEV.md).");

        await using var setup = NewContext();
        var migrator = setup.GetInfrastructure().GetRequiredService<IMigrator>();
        await migrator.MigrateAsync(Migration.InitialDatabase); // clean slate
        await migrator.MigrateAsync();

        var tenant = new Tenant { Name = "Escritório A", Slug = "escritorio-a" };
        setup.Tenants.Add(tenant);
        await setup.SaveChangesAsync();
        _tenantId = tenant.Id;

        var user = new UserAccount
        {
            TenantId = _tenantId,
            ExternalSubject = "oid-123",
            Upn = "user@escritorio-a.example",
            AdObjectSid = "S-1-5-21-0-0-0-1001",
        };
        setup.UserAccounts.Add(user);
        await setup.SaveChangesAsync();
        _userAccountId = user.Id;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private AppBridgeDbContext NewContext(params IInterceptor[] interceptors)
    {
        var builder = new DbContextOptionsBuilder<AppBridgeDbContext>().UseNpgsql(_connectionString);
        if (interceptors.Length > 0)
        {
            builder.AddInterceptors(interceptors);
        }

        return new AppBridgeDbContext(builder.Options, new TenantContext { TenantId = _tenantId });
    }

    private AccessEvent NewLoginEvent() => new()
    {
        TenantId = _tenantId,
        UserAccountId = _userAccountId,
        EventType = "login",
        Result = AccessEventResult.Success,
        OccurredAt = DateTimeOffset.UtcNow,
        CorrelationId = Guid.NewGuid(),
    };

    [Fact]
    public async Task ExecuteAsync_commits_the_audit_entry_and_the_grant_together()
    {
        await using var context = NewContext();
        var writer = new AuditWriter(context, NullLogger<AuditWriter>.Instance);
        var loginAt = DateTimeOffset.UtcNow;

        await writer.ExecuteAsync(
            NewLoginEvent(),
            ctx => ctx.UserAccounts.Single(u => u.Id == _userAccountId).LastLoginAt = loginAt);

        await using var verify = NewContext();
        var accessEvent = await verify.AccessEvents.SingleAsync(e => e.UserAccountId == _userAccountId);
        var user = await verify.UserAccounts.SingleAsync(u => u.Id == _userAccountId);

        Assert.Equal("login", accessEvent.EventType);
        // PostgreSQL timestamptz rounds to microseconds; .NET ticks are finer — compare with tolerance.
        Assert.Equal(loginAt, user.LastLoginAt!.Value, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task ExecuteAsync_denies_the_operation_when_the_write_fails()
    {
        await using var throwingContext = NewContext(new ThrowingSaveChangesInterceptor());
        var writer = new AuditWriter(throwingContext, NullLogger<AuditWriter>.Instance);
        var loginAt = DateTimeOffset.UtcNow;

        await Assert.ThrowsAsync<AuditWriteFailedException>(() => writer.ExecuteAsync(
            NewLoginEvent(),
            ctx => ctx.UserAccounts.Single(u => u.Id == _userAccountId).LastLoginAt = loginAt));

        // Neither half of the operation reached the database — not just the audit row.
        await using var verify = NewContext();
        Assert.Empty(await verify.AccessEvents.Where(e => e.UserAccountId == _userAccountId).ToListAsync());
        var user = await verify.UserAccounts.SingleAsync(u => u.Id == _userAccountId);
        Assert.Null(user.LastLoginAt);
    }

    [Fact]
    public async Task ExecuteAsync_logs_the_failure_before_rethrowing()
    {
        var recordingLogger = new RecordingLogger<AuditWriter>();
        await using var throwingContext = NewContext(new ThrowingSaveChangesInterceptor());
        var writer = new AuditWriter(throwingContext, recordingLogger);

        await Assert.ThrowsAsync<AuditWriteFailedException>(() => writer.ExecuteAsync(
            NewLoginEvent(),
            _ => { }));

        var errorEntry = Assert.Single(recordingLogger.Entries, e => e.LogLevel == LogLevel.Error);
        Assert.NotNull(errorEntry.Exception);
        Assert.Contains("AccessEvent", errorEntry.Message);
    }

    /// <summary>Throws exactly where EF Core would otherwise issue SQL — simulates the database write itself failing.</summary>
    private sealed class ThrowingSaveChangesInterceptor : SaveChangesInterceptor
    {
        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
            => throw new TimeoutException("Simulated database write failure (T-205 test double).");

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
            => throw new TimeoutException("Simulated database write failure (T-205 test double).");
    }

    private sealed record CapturedLogEntry(LogLevel LogLevel, string Message, Exception? Exception);

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        private readonly List<CapturedLogEntry> _entries = [];
        public IReadOnlyList<CapturedLogEntry> Entries => _entries;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => _entries.Add(new CapturedLogEntry(logLevel, formatter(state, exception), exception));
    }
}
