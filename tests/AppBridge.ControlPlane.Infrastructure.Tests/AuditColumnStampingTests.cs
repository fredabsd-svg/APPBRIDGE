using AppBridge.ControlPlane.Domain.Tenancy;
using AppBridge.ControlPlane.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AppBridge.ControlPlane.Infrastructure.Tests;

/// <summary>
/// Regression test for a real bug found during T-303: <c>CreatedAt</c>/<c>UpdatedAt</c> are
/// <c>init</c>-only on <see cref="AppBridge.ControlPlane.Domain.Common.AuditedEntity"/> and
/// <see cref="AppBridge.ControlPlane.Domain.Common.AppendOnlyEntity"/>, and nothing was setting
/// <c>CreatedAt</c> anywhere — every insert silently persisted <c>DateTimeOffset.MinValue</c>
/// (Npgsql's <c>-infinity</c> for <c>timestamptz</c>) until this was caught inspecting
/// <c>refresh_token.created_at</c> by hand. Fixed with a <c>SaveChanges</c> override in
/// <c>AppBridgeDbContext</c> that stamps both columns from the change tracker, not by asking every
/// call site to remember (the same "mechanism, not discipline" reasoning already applied to the
/// tenant filter, the composite foreign keys, and <c>IAuditWriter</c>).
/// </summary>
public sealed class AuditColumnStampingTests : IAsyncLifetime
{
    private string _connectionString = null!;
    private AppBridgeDbContext _context = null!;

    public async Task InitializeAsync()
    {
        _connectionString = Environment.GetEnvironmentVariable("APPBRIDGE_TEST_DB_CONNECTION")
            ?? throw new InvalidOperationException(
                "Set APPBRIDGE_TEST_DB_CONNECTION before running the Infrastructure tests (see docs/SETUP-DEV.md).");

        _context = NewContext();

        var migrator = _context.GetInfrastructure().GetRequiredService<IMigrator>();
        await migrator.MigrateAsync(Migration.InitialDatabase); // clean slate
        await migrator.MigrateAsync();
    }

    private AppBridgeDbContext NewContext() => new(
        new DbContextOptionsBuilder<AppBridgeDbContext>().UseNpgsql(_connectionString).Options,
        new TenantContext());

    public async Task DisposeAsync() => await _context.DisposeAsync();

    [Fact]
    public async Task Inserting_an_entity_stamps_CreatedAt_to_now_not_the_default_DateTimeOffset()
    {
        var before = DateTimeOffset.UtcNow;
        var tenant = new Tenant { Name = "Escritório A", Slug = "escritorio-a" };

        _context.Tenants.Add(tenant);
        await _context.SaveChangesAsync();

        var after = DateTimeOffset.UtcNow;

        Assert.InRange(tenant.CreatedAt, before.AddSeconds(-1), after.AddSeconds(1));
        Assert.Null(tenant.UpdatedAt); // never touched — stays null, not "also stamped"
    }

    [Fact]
    public async Task Modifying_an_entity_stamps_UpdatedAt_without_disturbing_CreatedAt()
    {
        var tenant = new Tenant { Name = "Escritório A", Slug = "escritorio-a" };
        _context.Tenants.Add(tenant);
        await _context.SaveChangesAsync();
        var createdAt = tenant.CreatedAt;

        var beforeUpdate = DateTimeOffset.UtcNow;
        tenant.Name = "Escritório A — renomeado";
        await _context.SaveChangesAsync();
        var afterUpdate = DateTimeOffset.UtcNow;

        Assert.Equal(createdAt, tenant.CreatedAt); // untouched by the update
        Assert.NotNull(tenant.UpdatedAt);
        Assert.InRange(tenant.UpdatedAt!.Value, beforeUpdate.AddSeconds(-1), afterUpdate.AddSeconds(1));
    }

    [Fact]
    public async Task Stamped_CreatedAt_round_trips_through_PostgreSQL_correctly()
    {
        // The bug this regresses against wrote MinValue, which Npgsql maps to -infinity — a value
        // that "round-trips" without error, so only reading it back and checking it's sane catches
        // this; a query that merely doesn't throw would have passed even with the bug present.
        var tenant = new Tenant { Name = "Escritório A", Slug = "escritorio-a" };
        _context.Tenants.Add(tenant);
        await _context.SaveChangesAsync();

        await using var verify = NewContext();
        var reloaded = await verify.Tenants.SingleAsync(t => t.Id == tenant.Id);

        Assert.True(reloaded.CreatedAt > DateTimeOffset.UtcNow.AddMinutes(-5));
    }
}
