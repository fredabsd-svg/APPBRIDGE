using AppBridge.ControlPlane.Domain.Catalog;
using AppBridge.ControlPlane.Domain.Identity;
using AppBridge.ControlPlane.Domain.Sessions;
using AppBridge.ControlPlane.Domain.Tenancy;
using AppBridge.ControlPlane.Infrastructure.Sessions;
using AppBridge.ControlPlane.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AppBridge.ControlPlane.Infrastructure.Tests;

/// <summary>
/// T-602 acceptance criterion (stale_expired half — see <see cref="ISessionReconciler"/> for why
/// reconciled_missing isn't here): "Sessão encerrada fora do AppBridge é fechada em até um ciclo"
/// applied to the inactivity path. A background sweep has no single ambient tenant, so this is the
/// one place in the codebase that deliberately reads and closes sessions across more than one
/// tenant in a single call — real PostgreSQL, real cross-tenant data, not mocked.
/// </summary>
public sealed class SessionReconcilerTests : IAsyncLifetime
{
    private string _connectionString = null!;

    public async Task InitializeAsync()
    {
        _connectionString = Environment.GetEnvironmentVariable("APPBRIDGE_TEST_DB_CONNECTION")
            ?? throw new InvalidOperationException(
                "Set APPBRIDGE_TEST_DB_CONNECTION before running the Infrastructure tests (see docs/SETUP-DEV.md).");

        await using var setup = NewContext(Guid.Empty);
        var migrator = setup.GetInfrastructure().GetRequiredService<IMigrator>();
        await migrator.MigrateAsync(Migration.InitialDatabase); // clean slate
        await migrator.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private AppBridgeDbContext NewContext(Guid tenantId) => new(
        new DbContextOptionsBuilder<AppBridgeDbContext>().UseNpgsql(_connectionString).Options,
        new TenantContext { TenantId = tenantId });

    private sealed record Fixture(Guid TenantId, Guid HostId, Guid UserId);

    private async Task<Fixture> SeedTenantAsync(string slug)
    {
        await using var context = NewContext(Guid.Empty);

        var tenant = new Tenant { Name = slug, Slug = slug };
        context.Tenants.Add(tenant);
        await context.SaveChangesAsync();

        var hostPool = new HostPool { TenantId = tenant.Id, Name = $"Pool {slug}" };
        context.HostPools.Add(hostPool);
        await context.SaveChangesAsync();

        var host = new SessionHost { TenantId = tenant.Id, HostPoolId = hostPool.Id, Fqdn = $"rds.{slug}.local", Status = SessionHostStatus.Online };
        context.SessionHosts.Add(host);
        await context.SaveChangesAsync();

        var user = new UserAccount
        {
            TenantId = tenant.Id,
            ExternalSubject = $"oid-{slug}",
            Upn = $"user@{slug}.local",
            AdObjectSid = $"S-1-5-21-0-0-0-{slug.GetHashCode() & 0x7fff}",
            DisplayName = slug,
        };
        context.UserAccounts.Add(user);
        await context.SaveChangesAsync();

        return new Fixture(tenant.Id, host.Id, user.Id);
    }

    /// <summary>
    /// A second user on the same tenant/host — ix_session_active_per_user (T-601) allows at most
    /// one active session per (tenant, host, user), so two "simultaneously active" sessions in the
    /// same tenant for this test need two different users, not two rows for the same one.
    /// </summary>
    private async Task<Fixture> SeedSecondUserAsync(Fixture tenant, string slug)
    {
        await using var context = NewContext(tenant.TenantId);
        var user = new UserAccount
        {
            TenantId = tenant.TenantId,
            ExternalSubject = $"oid-{slug}",
            Upn = $"user@{slug}.local",
            AdObjectSid = $"S-1-5-21-0-0-0-{slug.GetHashCode() & 0x7fff}",
            DisplayName = slug,
        };
        context.UserAccounts.Add(user);
        await context.SaveChangesAsync();
        return tenant with { UserId = user.Id };
    }

    private async Task<Guid> SeedSessionAsync(Fixture fixture, DateTimeOffset lastSeenAt, DateTimeOffset? endedAt = null)
    {
        await using var context = NewContext(fixture.TenantId);
        var session = new Session
        {
            TenantId = fixture.TenantId,
            UserAccountId = fixture.UserId,
            SessionHostId = fixture.HostId,
            BackendSessionId = $"pending:{Guid.NewGuid()}",
            StartedAt = lastSeenAt,
            LastSeenAt = lastSeenAt,
            EndedAt = endedAt,
            EndReason = endedAt is null ? null : SessionEndReason.Logoff,
        };
        context.Sessions.Add(session);
        await context.SaveChangesAsync();
        return session.Id;
    }

    /// <summary>
    /// The DbContext's tenant filter and SessionReconciler's own <see cref="TenantContext"/>
    /// parameter must be the exact same instance — production DI resolves both from one scope
    /// (Program.cs), and SessionReconciler relies on being able to retarget that shared instance
    /// per session as it sweeps across tenants. Building them separately (two different
    /// TenantContext objects) would silently break that: mutating one wouldn't affect the other,
    /// and every CancelSessionAsync call below would filter against the wrong tenant.
    /// </summary>
    private (AppBridgeDbContext Context, SessionReconciler Reconciler) NewReconciler(TimeSpan? inactivityWindow = null)
    {
        var tenantContext = new TenantContext();
        var context = new AppBridgeDbContext(
            new DbContextOptionsBuilder<AppBridgeDbContext>().UseNpgsql(_connectionString).Options, tenantContext);
        var reconciler = new SessionReconciler(
            context, new RdsSessionBackend(context), tenantContext,
            new SessionReconcilerOptions { InactivityWindow = inactivityWindow ?? TimeSpan.FromHours(12) });
        return (context, reconciler);
    }

    [Fact]
    public async Task Closes_a_session_whose_LastSeenAt_is_older_than_the_inactivity_window()
    {
        var tenant = await SeedTenantAsync("escritorio-a");
        var sessionId = await SeedSessionAsync(tenant, DateTimeOffset.UtcNow - TimeSpan.FromHours(13));

        var (context, reconciler) = NewReconciler();
        await using var _ = context;
        var closed = await reconciler.ReconcileStaleSessionsAsync();

        Assert.Equal(1, closed);

        await using var verify = NewContext(tenant.TenantId);
        var session = await verify.Sessions.SingleAsync(s => s.Id == sessionId);
        Assert.NotNull(session.EndedAt);
        Assert.Equal(SessionEndReason.StaleExpired, session.EndReason);
    }

    [Fact]
    public async Task Does_not_close_a_session_seen_within_the_inactivity_window()
    {
        var tenant = await SeedTenantAsync("escritorio-a");
        var sessionId = await SeedSessionAsync(tenant, DateTimeOffset.UtcNow - TimeSpan.FromHours(1));

        var (context, reconciler) = NewReconciler();
        await using var _ = context;
        var closed = await reconciler.ReconcileStaleSessionsAsync();

        Assert.Equal(0, closed);

        await using var verify = NewContext(tenant.TenantId);
        var session = await verify.Sessions.SingleAsync(s => s.Id == sessionId);
        Assert.Null(session.EndedAt);
    }

    [Fact]
    public async Task Does_not_touch_an_already_ended_session()
    {
        var tenant = await SeedTenantAsync("escritorio-a");
        var endedAt = DateTimeOffset.UtcNow - TimeSpan.FromHours(20);
        var sessionId = await SeedSessionAsync(tenant, DateTimeOffset.UtcNow - TimeSpan.FromHours(20), endedAt);

        var (context, reconciler) = NewReconciler();
        await using var _ = context;
        var closed = await reconciler.ReconcileStaleSessionsAsync();

        Assert.Equal(0, closed);

        await using var verify = NewContext(tenant.TenantId);
        var session = await verify.Sessions.SingleAsync(s => s.Id == sessionId);
        // PostgreSQL's timestamptz round-trips at microsecond precision, .NET DateTimeOffset at
        // 100ns ticks — a same-value comparison after a round trip needs tolerance, not exactness.
        Assert.Equal(endedAt, session.EndedAt!.Value, TimeSpan.FromSeconds(1));
        Assert.Equal(SessionEndReason.Logoff, session.EndReason); // untouched, not overwritten
    }

    [Fact]
    public async Task Sweeps_stale_sessions_across_more_than_one_tenant_in_a_single_call()
    {
        var tenantA = await SeedTenantAsync("escritorio-a2");
        var tenantB = await SeedTenantAsync("escritorio-b2");
        var tenantBSecondUser = await SeedSecondUserAsync(tenantB, "escritorio-b2-user2");
        var staleInA = await SeedSessionAsync(tenantA, DateTimeOffset.UtcNow - TimeSpan.FromHours(24));
        var staleInB = await SeedSessionAsync(tenantB, DateTimeOffset.UtcNow - TimeSpan.FromHours(24));
        var freshInB = await SeedSessionAsync(tenantBSecondUser, DateTimeOffset.UtcNow - TimeSpan.FromMinutes(5));

        var (context, reconciler) = NewReconciler();
        await using var _ = context;
        var closed = await reconciler.ReconcileStaleSessionsAsync();

        Assert.Equal(2, closed);

        await using var verifyA = NewContext(tenantA.TenantId);
        Assert.NotNull((await verifyA.Sessions.SingleAsync(s => s.Id == staleInA)).EndedAt);

        await using var verifyB = NewContext(tenantB.TenantId);
        Assert.NotNull((await verifyB.Sessions.SingleAsync(s => s.Id == staleInB)).EndedAt);
        Assert.Null((await verifyB.Sessions.SingleAsync(s => s.Id == freshInB)).EndedAt);
    }

    [Fact]
    public async Task No_stale_sessions_returns_zero_and_changes_nothing()
    {
        var tenant = await SeedTenantAsync("escritorio-a3");
        await SeedSessionAsync(tenant, DateTimeOffset.UtcNow);

        var (context, reconciler) = NewReconciler();
        await using var _ = context;
        var closed = await reconciler.ReconcileStaleSessionsAsync();

        Assert.Equal(0, closed);
    }
}
