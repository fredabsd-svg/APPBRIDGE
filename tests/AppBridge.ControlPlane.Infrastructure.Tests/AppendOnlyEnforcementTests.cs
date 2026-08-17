using AppBridge.ControlPlane.Domain.Catalog;
using AppBridge.ControlPlane.Domain.Identity;
using AppBridge.ControlPlane.Domain.Sessions;
using AppBridge.ControlPlane.Domain.Tenancy;
using AppBridge.ControlPlane.Domain.Trail;
using AppBridge.ControlPlane.Infrastructure.Auditing;
using AppBridge.ControlPlane.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AppBridge.ControlPlane.Infrastructure.Tests;

/// <summary>
/// T-701 acceptance criterion: "Sem caminho de UPDATE/DELETE na aplicação (RNF-019)". Real
/// PostgreSQL — the point is proving the row on disk never changes, not just that an exception is
/// thrown in memory.
/// </summary>
public sealed class AppendOnlyEnforcementTests : IAsyncLifetime
{
    private string _connectionString = null!;
    private Guid _tenantId;
    private Guid _userAccountId;
    private Guid _applicationId;

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
            ExternalSubject = "oid-ana",
            Upn = "ana@escritorio-a.local",
            AdObjectSid = "S-1-5-21-0-0-0-1001",
        };
        setup.UserAccounts.Add(user);
        await setup.SaveChangesAsync();
        _userAccountId = user.Id;

        var hostPool = new HostPool { TenantId = _tenantId, Name = "Pool A" };
        setup.HostPools.Add(hostPool);
        await setup.SaveChangesAsync();

        var application = new Application
        {
            TenantId = _tenantId,
            DisplayName = "Domínio Contábil",
            RemoteAppAlias = "dominio-contabil",
            HostPoolId = hostPool.Id,
        };
        setup.Applications.Add(application);
        await setup.SaveChangesAsync();
        _applicationId = application.Id;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private AppBridgeDbContext NewContext() => new(
        new DbContextOptionsBuilder<AppBridgeDbContext>().UseNpgsql(_connectionString).Options,
        new TenantContext { TenantId = _tenantId });

    private async Task<Guid> SeedLaunchAsync()
    {
        await using var context = NewContext();
        var launch = new Launch
        {
            TenantId = _tenantId,
            UserAccountId = _userAccountId,
            ApplicationId = _applicationId,
            RequestedAt = DateTimeOffset.UtcNow,
            Outcome = LaunchOutcome.DeniedPermission,
            DenialReason = "permission_revoked",
            RdpExpiresAt = DateTimeOffset.UtcNow,
            CorrelationId = Guid.NewGuid(),
            Purpose = LaunchPurpose.UserInitiated,
        };
        context.Launches.Add(launch);
        await context.SaveChangesAsync();
        return launch.Id;
    }

    private async Task<Guid> SeedAccessEventAsync()
    {
        await using var context = NewContext();
        var accessEvent = new AccessEvent
        {
            TenantId = _tenantId,
            UserAccountId = _userAccountId,
            EventType = "login",
            Result = AccessEventResult.Success,
            OccurredAt = DateTimeOffset.UtcNow,
            CorrelationId = Guid.NewGuid(),
        };
        context.AccessEvents.Add(accessEvent);
        await context.SaveChangesAsync();
        return accessEvent.Id;
    }

    private async Task<Guid> SeedPurgeRunAsync()
    {
        await using var context = NewContext();
        var purgeRun = new PurgeRun
        {
            TenantId = _tenantId,
            Category = RetentionCategory.Access,
            CutoffDate = DateTimeOffset.UtcNow,
            RowsDeleted = 0,
            StartedAt = DateTimeOffset.UtcNow,
            Outcome = "completed",
        };
        context.PurgeRuns.Add(purgeRun);
        await context.SaveChangesAsync();
        return purgeRun.Id;
    }

    [Fact]
    public async Task Modifying_a_persisted_Launch_throws_and_leaves_the_row_unchanged()
    {
        var launchId = await SeedLaunchAsync();

        await using var context = NewContext();
        var launch = await context.Launches.SingleAsync(l => l.Id == launchId);
        launch.Outcome = LaunchOutcome.Granted; // trying to "fix" history after the fact

        await Assert.ThrowsAsync<AppendOnlyViolationException>(() => context.SaveChangesAsync());

        await using var verify = NewContext();
        Assert.Equal(LaunchOutcome.DeniedPermission, (await verify.Launches.SingleAsync(l => l.Id == launchId)).Outcome);
    }

    [Fact]
    public async Task Deleting_a_persisted_Launch_throws_and_the_row_still_exists()
    {
        var launchId = await SeedLaunchAsync();

        await using var context = NewContext();
        context.Launches.Remove(await context.Launches.SingleAsync(l => l.Id == launchId));

        await Assert.ThrowsAsync<AppendOnlyViolationException>(() => context.SaveChangesAsync());

        await using var verify = NewContext();
        Assert.NotNull(await verify.Launches.SingleOrDefaultAsync(l => l.Id == launchId));
    }

    [Fact]
    public async Task Modifying_a_persisted_AccessEvent_throws_and_leaves_the_row_unchanged()
    {
        var accessEventId = await SeedAccessEventAsync();

        await using var context = NewContext();
        var accessEvent = await context.AccessEvents.SingleAsync(e => e.Id == accessEventId);
        accessEvent.Result = AccessEventResult.Failure;

        await Assert.ThrowsAsync<AppendOnlyViolationException>(() => context.SaveChangesAsync());

        await using var verify = NewContext();
        Assert.Equal(AccessEventResult.Success, (await verify.AccessEvents.SingleAsync(e => e.Id == accessEventId)).Result);
    }

    [Fact]
    public async Task Deleting_a_persisted_AccessEvent_throws_and_the_row_still_exists()
    {
        var accessEventId = await SeedAccessEventAsync();

        await using var context = NewContext();
        context.AccessEvents.Remove(await context.AccessEvents.SingleAsync(e => e.Id == accessEventId));

        await Assert.ThrowsAsync<AppendOnlyViolationException>(() => context.SaveChangesAsync());

        await using var verify = NewContext();
        Assert.NotNull(await verify.AccessEvents.SingleOrDefaultAsync(e => e.Id == accessEventId));
    }

    [Fact]
    public async Task Modifying_a_persisted_PurgeRun_throws_and_leaves_the_row_unchanged()
    {
        var purgeRunId = await SeedPurgeRunAsync();

        await using var context = NewContext();
        var purgeRun = await context.PurgeRuns.SingleAsync(p => p.Id == purgeRunId);
        purgeRun.RowsDeleted = 999; // trying to rewrite the trail's own trail

        await Assert.ThrowsAsync<AppendOnlyViolationException>(() => context.SaveChangesAsync());

        await using var verify = NewContext();
        Assert.Equal(0, (await verify.PurgeRuns.SingleAsync(p => p.Id == purgeRunId)).RowsDeleted);
    }

    [Fact]
    public async Task Inserting_a_new_trail_row_is_unaffected_by_the_guard()
    {
        // Sanity: the guard must fire only on Modified/Deleted, never on Added — every other test
        // in this codebase that writes a Launch/AccessEvent already depends on this implicitly;
        // this makes it explicit.
        var launchId = await SeedLaunchAsync();
        Assert.NotEqual(Guid.Empty, launchId);
    }
}
