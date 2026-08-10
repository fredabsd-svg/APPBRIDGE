using AppBridge.ControlPlane.Domain.Catalog;
using AppBridge.ControlPlane.Domain.Identity;
using AppBridge.ControlPlane.Domain.Sessions;
using AppBridge.ControlPlane.Domain.Tenancy;
using AppBridge.ControlPlane.Domain.Trail;
using AppBridge.ControlPlane.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AppBridge.ControlPlane.Infrastructure.Tests;

/// <summary>
/// T-207 acceptance criterion: "Contagem de RF-062 não soma prelaunchs; teste cobre o caso"
/// (ADR-0016, Gap 1). The <c>purpose</c> column and <see cref="LaunchPurpose"/> enum were built in
/// T-202 alongside the rest of the schema; what was still missing was the test itself — this file
/// is that test, not a metering service (RF-062's actual endpoint is MVP-1, ADR-0006, and doesn't
/// exist yet). It proves the one thing a future metering query depends on: counting launches by
/// <c>purpose = user_initiated</c> excludes prelaunches, at the data layer that query will run
/// against.
/// </summary>
public sealed class LaunchPurposeMeteringTests : IAsyncLifetime
{
    private string _connectionString = null!;
    private Guid _tenantId;
    private Guid _applicationId;
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
        var userAccount = new UserAccount
        {
            TenantId = _tenantId,
            ExternalSubject = "oid-a",
            Upn = "a@escritorio-a.example",
            AdObjectSid = "S-1-5-21-0-0-0-1001",
        };
        setup.Applications.Add(application);
        setup.UserAccounts.Add(userAccount);
        await setup.SaveChangesAsync();
        _applicationId = application.Id;
        _userAccountId = userAccount.Id;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private AppBridgeDbContext NewContext() => new(
        new DbContextOptionsBuilder<AppBridgeDbContext>().UseNpgsql(_connectionString).Options,
        new TenantContext { TenantId = _tenantId });

    [Fact]
    public async Task Metering_count_of_launches_by_purpose_excludes_prelaunches()
    {
        await using (var context = NewContext())
        {
            context.Launches.AddRange(
                NewLaunch(LaunchPurpose.UserInitiated),
                NewLaunch(LaunchPurpose.UserInitiated),
                NewLaunch(LaunchPurpose.UserInitiated),
                NewLaunch(LaunchPurpose.Prelaunch),
                NewLaunch(LaunchPurpose.Prelaunch));
            await context.SaveChangesAsync();
        }

        await using var verify = NewContext();
        var totalLaunches = await verify.Launches.CountAsync();
        var meteredLaunches = await verify.Launches.CountAsync(l => l.Purpose == LaunchPurpose.UserInitiated);

        Assert.Equal(5, totalLaunches); // sanity: both purposes were actually written
        Assert.Equal(3, meteredLaunches); // RF-062 must never count a prelaunch as real use
    }

    private Launch NewLaunch(LaunchPurpose purpose) => new()
    {
        TenantId = _tenantId,
        UserAccountId = _userAccountId,
        ApplicationId = _applicationId,
        RequestedAt = DateTimeOffset.UtcNow,
        Outcome = LaunchOutcome.Granted,
        RdpExpiresAt = DateTimeOffset.UtcNow.AddSeconds(60),
        CorrelationId = Guid.NewGuid(),
        Purpose = purpose,
    };
}
