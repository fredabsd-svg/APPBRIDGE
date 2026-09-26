using AppBridge.ControlPlane.Data;
using AppBridge.ControlPlane.Domain.Entities;
using AppBridge.ControlPlane.Domain.Enums;
using AppBridge.ControlPlane.Launching;
using AppBridge.ControlPlane.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AppBridge.ControlPlane.Tests;

// T-601 · SessionRegistry (ADR-0021).
public sealed partial class TenantIsolationTests
{
    [Fact]
    public async Task Second_application_reuses_the_session_registered_by_the_first_launch()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var world = await SeedLaunchWorldAsync(grantPermission: true, addSession: false);
        var secondApplicationId = await AddPermittedApplicationAsync(world, "AppSegundo");
        await using var db = CreateContext(world.TenantId);
        var service = CreateLaunchService(db, CreateTenantContext(world.TenantId), CreateRdsBackend(db), new FakeRdpFileSigner());

        var first = await service.LaunchAsync(world.UserId,
            new LaunchRequest(world.ApplicationId, LaunchPurpose.UserInitiated, "workstation-a"),
            Guid.CreateVersion7(), "127.0.0.1", Guid.CreateVersion7(), cancellationToken);
        var second = await service.LaunchAsync(world.UserId,
            new LaunchRequest(secondApplicationId, LaunchPurpose.UserInitiated, "workstation-a"),
            Guid.CreateVersion7(), "127.0.0.1", Guid.CreateVersion7(), cancellationToken);

        Assert.Equal(StatusCodes.Status201Created, first.StatusCode);
        Assert.False(first.Response!.SessionReused);
        Assert.Equal(StatusCodes.Status201Created, second.StatusCode);
        Assert.True(second.Response!.SessionReused);

        var session = Assert.Single(await db.Sessions.ToListAsync(cancellationToken));
        Assert.Null(session.BackendSessionId);
        Assert.Null(session.EndedAt);
        Assert.Equal(world.Host.Id, session.SessionHostId);
        Assert.Equal("workstation-a", session.WorkstationName);
        var launches = await db.Launches.OrderBy(launch => launch.RequestedAt).ToListAsync(cancellationToken);
        Assert.Equal(2, launches.Count);
        Assert.All(launches, launch => Assert.Equal(session.Id, launch.SessionId));
    }

    [Fact]
    public async Task Concurrent_prelaunch_and_click_register_a_single_session()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var world = await SeedLaunchWorldAsync(grantPermission: true, addSession: false);
        await AddOnlineHostAsync(world, "rdsh02.example.test", maxSessions: 20);

        async Task<LaunchCommandResult> LaunchAsync(LaunchPurpose purpose)
        {
            await using var db = CreateContext(world.TenantId);
            var service = CreateLaunchService(db, CreateTenantContext(world.TenantId), CreateRdsBackend(db), new FakeRdpFileSigner());
            return await service.LaunchAsync(world.UserId,
                new LaunchRequest(world.ApplicationId, purpose, "workstation-a"),
                Guid.CreateVersion7(), "127.0.0.1", Guid.CreateVersion7(), cancellationToken);
        }

        var results = await Task.WhenAll(
            LaunchAsync(LaunchPurpose.Prelaunch),
            LaunchAsync(LaunchPurpose.UserInitiated));

        Assert.All(results, result => Assert.Equal(StatusCodes.Status201Created, result.StatusCode));
        Assert.Single(results, result => result.Response!.SessionReused);
        await using var verify = CreateContext(world.TenantId);
        var session = Assert.Single(await verify.Sessions.ToListAsync(cancellationToken));
        Assert.All(await verify.Launches.ToListAsync(cancellationToken), launch => Assert.Equal(session.Id, launch.SessionId));
    }

    [Fact]
    public async Task Pending_session_holds_capacity_only_inside_the_binding_window()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var world = await SeedLaunchWorldAsync(grantPermission: true, addSession: false);
        var otherUserId = await AddUserToWorldAsync(world, "other");
        await using (var seed = CreateContext(world.TenantId))
        {
            var host = await seed.SessionHosts.SingleAsync(row => row.Id == world.Host.Id, cancellationToken);
            host.MaxSessions = 1;
            seed.Sessions.Add(new RemoteSession
            {
                TenantId = world.TenantId,
                UserAccountId = otherUserId,
                SessionHostId = world.Host.Id,
                BackendSessionId = null,
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                LastSeenAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                SourceIp = "127.0.0.2",
                WorkstationName = "workstation-other"
            });
            await seed.SaveChangesAsync(cancellationToken);
        }

        await using var db = CreateContext(world.TenantId);
        var service = CreateLaunchService(db, CreateTenantContext(world.TenantId), CreateRdsBackend(db), new FakeRdpFileSigner());
        var request = new LaunchRequest(world.ApplicationId, LaunchPurpose.UserInitiated, "workstation-a");

        var full = await service.LaunchAsync(world.UserId, request, Guid.CreateVersion7(), "127.0.0.1", Guid.CreateVersion7(), cancellationToken);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, full.StatusCode);
        Assert.Equal("APPLICATION_UNAVAILABLE", full.ErrorCode);

        var pending = await db.Sessions.SingleAsync(row => row.UserAccountId == otherUserId, cancellationToken);
        pending.LastSeenAt = DateTimeOffset.UtcNow.AddMinutes(-11);
        await db.SaveChangesAsync(cancellationToken);

        var freed = await service.LaunchAsync(world.UserId, request, Guid.CreateVersion7(), "127.0.0.1", Guid.CreateVersion7(), cancellationToken);
        Assert.Equal(StatusCodes.Status201Created, freed.StatusCode);
        Assert.False(freed.Response!.SessionReused);
    }

    [Fact]
    public async Task Draining_host_keeps_the_existing_session_but_receives_no_new_one()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var world = await SeedLaunchWorldAsync(grantPermission: true, addSession: true);
        await using var db = CreateContext(world.TenantId);
        var host = await db.SessionHosts.SingleAsync(row => row.Id == world.Host.Id, cancellationToken);
        host.Status = SessionHostStatus.Draining;
        await db.SaveChangesAsync(cancellationToken);
        var service = CreateLaunchService(db, CreateTenantContext(world.TenantId), CreateRdsBackend(db), new FakeRdpFileSigner());
        var request = new LaunchRequest(world.ApplicationId, LaunchPurpose.UserInitiated, "workstation-a");

        var reused = await service.LaunchAsync(world.UserId, request, Guid.CreateVersion7(), "127.0.0.1", Guid.CreateVersion7(), cancellationToken);
        Assert.True(reused.Response!.SessionReused);
        Assert.Equal(world.SessionId, (await db.Launches.SingleAsync(cancellationToken)).SessionId);

        var bound = await db.Sessions.SingleAsync(row => row.Id == world.SessionId, cancellationToken);
        bound.EndedAt = DateTimeOffset.UtcNow;
        bound.EndReason = SessionEndReason.Logoff;
        await db.SaveChangesAsync(cancellationToken);

        var refused = await service.LaunchAsync(world.UserId, request, Guid.CreateVersion7(), "127.0.0.1", Guid.CreateVersion7(), cancellationToken);
        Assert.Equal("APPLICATION_UNAVAILABLE", refused.ErrorCode);
    }

    [Fact]
    public async Task Signing_failure_registers_no_session()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var world = await SeedLaunchWorldAsync(grantPermission: true, addSession: false);
        await using var db = CreateContext(world.TenantId);
        var service = CreateLaunchService(db, CreateTenantContext(world.TenantId), CreateRdsBackend(db), new FakeRdpFileSigner(fail: true));

        var result = await service.LaunchAsync(world.UserId,
            new LaunchRequest(world.ApplicationId, LaunchPurpose.UserInitiated, "workstation-a"),
            Guid.CreateVersion7(), "127.0.0.1", Guid.CreateVersion7(), cancellationToken);

        Assert.Equal("SIGNING_UNAVAILABLE", result.ErrorCode);
        Assert.Empty(await db.Sessions.ToListAsync(cancellationToken));
        Assert.Null((await db.Launches.SingleAsync(cancellationToken)).SessionId);
    }

    [Fact]
    public async Task Session_placement_requires_the_launch_transaction()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var world = await SeedLaunchWorldAsync(grantPermission: true, addSession: false);
        await using var db = CreateContext(world.TenantId);
        var application = await db.Applications.SingleAsync(cancellationToken);
        var user = await db.UserAccounts.SingleAsync(cancellationToken);
        var registry = new SessionRegistry(db, CreateTenantContext(world.TenantId), CreateRdsBackend(db),
            Microsoft.Extensions.Options.Options.Create(new SessionRegistryOptions()));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            registry.PlaceAsync(application, user, DateTimeOffset.UtcNow, cancellationToken));
        Assert.Equal(TimeSpan.FromMinutes(60), new SessionRegistryOptions { PendingBindingMinutes = 600 }.PendingBindingWindow);
        Assert.Equal(TimeSpan.FromMinutes(1), new SessionRegistryOptions { PendingBindingMinutes = 0 }.PendingBindingWindow);
    }

    [Fact]
    public async Task Rds_cancellation_closes_an_unbound_session_without_calling_the_host()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var world = await SeedLaunchWorldAsync(grantPermission: true, addSession: true);
        await using var db = CreateContext(world.TenantId);
        var session = await db.Sessions.SingleAsync(row => row.Id == world.SessionId, cancellationToken);
        session.BackendSessionId = null;
        await db.SaveChangesAsync(cancellationToken);

        await CreateRdsBackend(db).CancelSessionAsync(world.SessionId, "prelaunch_failed", cancellationToken);

        Assert.NotNull(session.EndedAt);
        Assert.Equal(SessionEndReason.Logoff, session.EndReason);
    }

    private async Task<Guid> AddPermittedApplicationAsync(LaunchWorld world, string alias)
    {
        await using var db = CreateContext(world.TenantId);
        var permission = await db.ApplicationPermissions.SingleAsync(TestContext.Current.CancellationToken);
        var application = new RemoteApplication
        {
            TenantId = world.TenantId,
            HostPoolId = world.Host.HostPoolId,
            DisplayName = $"Aplicativo {alias}",
            RemoteAppAlias = alias,
            LaunchMode = ApplicationLaunchMode.RemoteApp,
            Status = ApplicationStatus.Published
        };
        db.Applications.Add(application);
        db.ApplicationPermissions.Add(new ApplicationPermission
        {
            TenantId = world.TenantId,
            ApplicationId = application.Id,
            GroupId = permission.GroupId,
            EffectiveFrom = permission.EffectiveFrom,
            GrantedBy = world.UserId
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return application.Id;
    }

    private async Task AddOnlineHostAsync(LaunchWorld world, string fqdn, int maxSessions)
    {
        await using var db = CreateContext(world.TenantId);
        db.SessionHosts.Add(new SessionHost
        {
            TenantId = world.TenantId,
            HostPoolId = world.Host.HostPoolId,
            Fqdn = fqdn,
            Status = SessionHostStatus.Online,
            MaxSessions = maxSessions
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Guid> AddUserToWorldAsync(LaunchWorld world, string name)
    {
        await using var db = CreateContext(world.TenantId);
        var membership = await db.UserGroupMemberships.SingleAsync(TestContext.Current.CancellationToken);
        var user = new UserAccount
        {
            TenantId = world.TenantId,
            ExternalSubject = $"{name}-{world.TenantId:N}",
            Upn = $"{name}@example.test",
            AdObjectSid = $"S-1-5-21-{name}-{world.TenantId:N}",
            DisplayName = name,
            Email = $"{name}@example.test",
            Status = UserAccountStatus.Active
        };
        db.UserAccounts.Add(user);
        db.UserGroupMemberships.Add(new UserGroupMembership
        {
            TenantId = world.TenantId,
            UserAccountId = user.Id,
            GroupId = membership.GroupId,
            Source = GroupSource.Local
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return user.Id;
    }
}
