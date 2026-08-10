using AppBridge.ControlPlane.Domain.Catalog;
using AppBridge.ControlPlane.Domain.Identity;
using AppBridge.ControlPlane.Domain.Sessions;
using AppBridge.ControlPlane.Domain.Tenancy;
using AppBridge.ControlPlane.Infrastructure.Authorization;
using AppBridge.ControlPlane.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AppBridge.ControlPlane.Infrastructure.Tests;

/// <summary>
/// T-304 acceptance criterion (V-07, RNF-030): "Revogar acesso e cronometrar a negativa do
/// lançamento seguinte (≤ 60 s)". <see cref="AuthorizationService"/> has no caching layer — it
/// reads <c>ApplicationPermission</c> vigência fresh on every call — so these tests prove denial
/// on the very next check after revocation, with no wall-clock wait needed: that immediacy is
/// what makes the 60 s bound trivially true rather than something to race against.
/// </summary>
public sealed class AuthorizationServiceTests : IAsyncLifetime
{
    private string _connectionString = null!;
    private Guid _tenantId;
    private Guid _userAccountId;
    private Guid _applicationId;
    private Guid _otherApplicationId;
    private Guid _groupId;

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
        var otherApplication = new Application
        {
            TenantId = _tenantId,
            DisplayName = "Alterdata",
            RemoteAppAlias = "alterdata",
            HostPoolId = hostPool.Id,
        };
        var userAccount = new UserAccount
        {
            TenantId = _tenantId,
            ExternalSubject = "oid-ana",
            Upn = "ana@escritorio-a.local",
            AdObjectSid = "S-1-5-21-0-0-0-1001",
            DisplayName = "Ana Souza",
        };
        var admin = new UserAccount
        {
            TenantId = _tenantId,
            ExternalSubject = "oid-admin",
            Upn = "admin@escritorio-a.local",
            AdObjectSid = "S-1-5-21-0-0-0-1000",
            DisplayName = "Admin",
        };
        var group = new Group { TenantId = _tenantId, Name = "Contabilidade" };
        setup.Applications.AddRange(application, otherApplication);
        setup.UserAccounts.AddRange(userAccount, admin);
        setup.Groups.Add(group);
        await setup.SaveChangesAsync();
        _applicationId = application.Id;
        _otherApplicationId = otherApplication.Id;
        _userAccountId = userAccount.Id;
        _groupId = group.Id;
        _grantedBy = admin.Id;

        setup.UserGroupMemberships.Add(new UserGroupMembership
        {
            TenantId = _tenantId,
            UserAccountId = _userAccountId,
            GroupId = _groupId,
        });
        await setup.SaveChangesAsync();
    }

    private Guid _grantedBy;

    public Task DisposeAsync() => Task.CompletedTask;

    private AppBridgeDbContext NewContext() => new(
        new DbContextOptionsBuilder<AppBridgeDbContext>().UseNpgsql(_connectionString).Options,
        new TenantContext { TenantId = _tenantId });

    private ApplicationPermission NewPermission(
        Guid applicationId, DateTimeOffset effectiveFrom, DateTimeOffset? effectiveTo = null) => new()
    {
        TenantId = _tenantId,
        ApplicationId = applicationId,
        GroupId = _groupId,
        EffectiveFrom = effectiveFrom,
        EffectiveTo = effectiveTo,
        GrantedBy = _grantedBy,
    };

    [Fact]
    public async Task Permission_within_its_effective_window_grants_access()
    {
        await using (var context = NewContext())
        {
            context.ApplicationPermissions.Add(
                NewPermission(_applicationId, DateTimeOffset.UtcNow.AddDays(-1)));
            await context.SaveChangesAsync();
        }

        await using var verify = NewContext();
        var service = new AuthorizationService(verify);
        Assert.True(await service.HasActivePermissionAsync(_userAccountId, _applicationId));
    }

    [Fact]
    public async Task Revoking_a_permission_denies_the_very_next_check()
    {
        await using (var context = NewContext())
        {
            context.ApplicationPermissions.Add(
                NewPermission(_applicationId, DateTimeOffset.UtcNow.AddDays(-1)));
            await context.SaveChangesAsync();
        }

        await using (var before = NewContext())
        {
            Assert.True(await new AuthorizationService(before).HasActivePermissionAsync(_userAccountId, _applicationId));
        }

        await using (var revoke = NewContext())
        {
            var permission = await revoke.ApplicationPermissions.SingleAsync();
            permission.EffectiveTo = DateTimeOffset.UtcNow;
            permission.RevokedBy = _grantedBy;
            permission.RevocationReason = "Desligamento";
            await revoke.SaveChangesAsync();
        }

        await using var after = NewContext();
        Assert.False(await new AuthorizationService(after).HasActivePermissionAsync(_userAccountId, _applicationId));
    }

    [Fact]
    public async Task No_permission_at_all_denies_access()
    {
        await using var context = NewContext();
        var service = new AuthorizationService(context);
        Assert.False(await service.HasActivePermissionAsync(_userAccountId, _applicationId));
    }

    [Fact]
    public async Task Permission_for_a_different_application_does_not_grant_access()
    {
        await using (var context = NewContext())
        {
            context.ApplicationPermissions.Add(
                NewPermission(_otherApplicationId, DateTimeOffset.UtcNow.AddDays(-1)));
            await context.SaveChangesAsync();
        }

        await using var verify = NewContext();
        var service = new AuthorizationService(verify);
        Assert.False(await service.HasActivePermissionAsync(_userAccountId, _applicationId));
    }

    [Fact]
    public async Task Permission_not_yet_effective_denies_access()
    {
        await using (var context = NewContext())
        {
            context.ApplicationPermissions.Add(
                NewPermission(_applicationId, DateTimeOffset.UtcNow.AddDays(1)));
            await context.SaveChangesAsync();
        }

        await using var verify = NewContext();
        var service = new AuthorizationService(verify);
        Assert.False(await service.HasActivePermissionAsync(_userAccountId, _applicationId));
    }

    [Fact]
    public async Task Permission_already_revoked_in_the_past_denies_access()
    {
        await using (var context = NewContext())
        {
            context.ApplicationPermissions.Add(NewPermission(
                _applicationId,
                DateTimeOffset.UtcNow.AddDays(-2),
                DateTimeOffset.UtcNow.AddDays(-1)));
            await context.SaveChangesAsync();
        }

        await using var verify = NewContext();
        var service = new AuthorizationService(verify);
        Assert.False(await service.HasActivePermissionAsync(_userAccountId, _applicationId));
    }
}
