using AppBridge.ControlPlane.Data;
using AppBridge.ControlPlane.Domain.Entities;
using AppBridge.ControlPlane.Domain.Enums;
using AppBridge.ControlPlane.Launching;
using AppBridge.ControlPlane.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AppBridge.ControlPlane.Tests;

// T-602 · SessionReconciler (ADR-0022).
public sealed partial class TenantIsolationTests
{
    [Fact]
    public async Task Reconciler_binds_a_pending_session_refreshes_it_and_closes_it_when_it_disappears()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var world = await SeedLaunchWorldAsync(grantPermission: true, addSession: false);
        var sessionId = await AddPendingSessionAsync(world, world.UserId, DateTimeOffset.UtcNow.AddMinutes(-1));
        var userSid = $"S-1-5-21-{world.TenantId:N}";
        var backend = new ScriptedSessionBackend(
            new BackendSessionSnapshot("RDSH01.Example.Test.", "4", "S-1-5-21-someone-else", DateTimeOffset.UtcNow),
            new BackendSessionSnapshot("rdsh01.example.test", "7", userSid.ToUpperInvariant(), DateTimeOffset.UtcNow.AddSeconds(-30)),
            new BackendSessionSnapshot("rdsh01.example.test", "9", userSid, DateTimeOffset.UtcNow));

        var first = await ReconcileAsync(world.TenantId, backend, DateTimeOffset.UtcNow);

        Assert.Equal(new SessionReconciliationResult(true, 0, 1, 0, 2), first);
        await using (var db = CreateContext(world.TenantId))
        {
            var session = await db.Sessions.SingleAsync(row => row.Id == sessionId, cancellationToken);
            Assert.Equal("9", session.BackendSessionId);
            var started = await db.AccessEvents.SingleAsync(row => row.EventType == AccessEventType.SessionStarted, cancellationToken);
            Assert.Equal(world.UserId, started.UserAccountId);
            Assert.Equal("workstation-a", started.WorkstationName);
            Assert.Equal(sessionId, started.Payload!.RootElement.GetProperty("sessionId").GetGuid());
        }

        var refreshedAt = DateTimeOffset.UtcNow.AddMinutes(1);
        var second = await ReconcileAsync(world.TenantId, backend, refreshedAt);
        Assert.Equal(1, second.Refreshed);
        Assert.Equal(0, second.Bound);

        backend.Sessions = [];
        var third = await ReconcileAsync(world.TenantId, backend, DateTimeOffset.UtcNow.AddMinutes(2));
        Assert.Equal(1, third.Closed);
        await using (var db = CreateContext(world.TenantId))
        {
            var session = await db.Sessions.SingleAsync(row => row.Id == sessionId, cancellationToken);
            Assert.Equal(SessionEndReason.ReconciledMissing, session.EndReason);
            Assert.NotNull(session.EndedAt);
            var ended = await db.AccessEvents.SingleAsync(row => row.EventType == AccessEventType.SessionEnded, cancellationToken);
            Assert.Equal("reconciled_missing", ended.FailureReason);
            Assert.True(ended.Payload!.RootElement.GetProperty("durationSeconds").GetInt64() >= 60);
            Assert.Equal(world.Host.Id, ended.Payload.RootElement.GetProperty("sessionHostId").GetGuid());
        }
    }

    [Fact]
    public async Task Reconciler_closes_a_pending_session_that_never_connected_only_after_the_window()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var world = await SeedLaunchWorldAsync(grantPermission: true, addSession: false);
        var fresh = await AddPendingSessionAsync(world, world.UserId, DateTimeOffset.UtcNow.AddMinutes(-2));
        var otherUserId = await AddUserToWorldAsync(world, "late");
        var expired = await AddPendingSessionAsync(world, otherUserId, DateTimeOffset.UtcNow.AddMinutes(-11));
        var backend = new ScriptedSessionBackend(
            new BackendSessionSnapshot("rdsh01.example.test", "3", "S-1-5-21-unrelated", null));

        var result = await ReconcileAsync(world.TenantId, backend, DateTimeOffset.UtcNow);

        Assert.Equal(1, result.Closed);
        Assert.Equal(1, result.Unadopted);
        await using var db = CreateContext(world.TenantId);
        Assert.Null((await db.Sessions.SingleAsync(row => row.Id == fresh, cancellationToken)).EndedAt);
        var closed = await db.Sessions.SingleAsync(row => row.Id == expired, cancellationToken);
        Assert.Equal(SessionEndReason.ReconciledMissing, closed.EndReason);
        Assert.Equal("never_connected",
            (await db.AccessEvents.SingleAsync(row => row.EventType == AccessEventType.SessionEnded, cancellationToken)).FailureReason);
    }

    [Fact]
    public async Task Reconciler_without_backend_expires_only_stale_sessions()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var world = await SeedLaunchWorldAsync(grantPermission: true, addSession: true);
        var staleUserId = await AddUserToWorldAsync(world, "stale");
        var stale = await AddPendingSessionAsync(world, staleUserId, DateTimeOffset.UtcNow.AddMinutes(-31));
        var backend = new ScriptedSessionBackend { Failure = new RdsSessionException("broker offline") };

        var result = await ReconcileAsync(world.TenantId, backend, DateTimeOffset.UtcNow);

        Assert.Equal(new SessionReconciliationResult(false, 0, 0, 1, 0), result);
        await using var db = CreateContext(world.TenantId);
        Assert.Null((await db.Sessions.SingleAsync(row => row.Id == world.SessionId, cancellationToken)).EndedAt);
        Assert.Equal(SessionEndReason.StaleExpired,
            (await db.Sessions.SingleAsync(row => row.Id == stale, cancellationToken)).EndReason);
    }

    [Fact]
    public async Task Reconciler_cycle_visits_only_active_tenants()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var active = await SeedLaunchWorldAsync(grantPermission: true, addSession: true);
        var suspended = await SeedLaunchWorldAsync(grantPermission: true, addSession: true);
        await using (var db = CreateContext(suspended.TenantId))
        {
            var tenant = await db.Tenants.SingleAsync(cancellationToken);
            tenant.Status = TenantStatus.Suspended;
            await db.SaveChangesAsync(cancellationToken);
        }

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<TenantContext>();
        services.AddScoped(provider => CreateContext(provider.GetRequiredService<TenantContext>()));
        services.AddSingleton<ISessionBackend>(new ScriptedSessionBackend());
        services.AddSingleton(Options.Create(new SessionRegistryOptions()));
        services.AddSingleton(Options.Create(new SessionReconcilerOptions()));
        services.AddScoped<SessionReconciliationService>();
        await using var provider = services.BuildServiceProvider();
        var reconciler = new SessionReconciler(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new SessionReconcilerOptions()),
            NullLogger<SessionReconciler>.Instance);

        await reconciler.RunCycleAsync(cancellationToken);

        await using (var db = CreateContext(active.TenantId))
        {
            Assert.Equal(SessionEndReason.ReconciledMissing,
                (await db.Sessions.SingleAsync(row => row.Id == active.SessionId, cancellationToken)).EndReason);
        }

        await using (var db = CreateContext(suspended.TenantId))
        {
            Assert.Null((await db.Sessions.SingleAsync(row => row.Id == suspended.SessionId, cancellationToken)).EndedAt);
        }
    }

    [Fact]
    public void Rds_session_list_parser_accepts_powershell_shapes_and_rejects_bad_rows()
    {
        var list = RdsSessionListParser.Parse(
            """[{"host":"rdsh01.example.test","id":"2","sid":"S-1-5-21-1","created":"2026-09-26T10:00:00.0000000Z"},{"host":"rdsh02.example.test","id":"5","sid":null,"created":null}]""");
        Assert.Equal(2, list.Count);
        Assert.Equal(new DateTimeOffset(2026, 9, 26, 10, 0, 0, TimeSpan.Zero), list[0].CreatedAt);
        Assert.Null(list[1].UserSid);
        Assert.Null(list[1].CreatedAt);

        Assert.Single(RdsSessionListParser.Parse("""{"host":"rdsh01.example.test","id":"2","sid":"S-1-5-21-1"}"""));
        Assert.Empty(RdsSessionListParser.Parse(""));
        Assert.Empty(RdsSessionListParser.Parse("null"));
        Assert.Empty(RdsSessionListParser.Parse("[]"));
        Assert.Throws<RdsSessionException>(() => RdsSessionListParser.Parse("not json"));
        Assert.Throws<RdsSessionException>(() => RdsSessionListParser.Parse("42"));
        Assert.Throws<RdsSessionException>(() => RdsSessionListParser.Parse("""[{"host":"rdsh01","id":"abc"}]"""));
        Assert.Throws<RdsSessionException>(() => RdsSessionListParser.Parse("""[{"id":"2"}]"""));
        Assert.Equal("rdsh01.example.test", RdsSessionListParser.NormalizeHost(" RDSH01.Example.Test. "));
    }

    [Fact]
    public async Task Rds_backend_listing_requires_a_valid_connection_broker()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var world = await SeedLaunchWorldAsync(grantPermission: true, addSession: false);
        await using var db = CreateContext(world.TenantId);

        await Assert.ThrowsAsync<RdsSessionException>(() =>
            CreateRdsBackend(db).ListActiveSessionsAsync([world.Host], cancellationToken));

        var invalidBroker = new RdsSessionBackend(
            db,
            Options.Create(new RdsSessionOptions { ConnectionBroker = "broker'; Remove-Item C:\\" }),
            Options.Create(new SessionRegistryOptions()),
            NullLogger<RdsSessionBackend>.Instance);
        await Assert.ThrowsAsync<RdsSessionException>(() =>
            invalidBroker.ListActiveSessionsAsync([world.Host], cancellationToken));

        if (!OperatingSystem.IsWindows())
        {
            var configured = new RdsSessionBackend(
                db,
                Options.Create(new RdsSessionOptions { ConnectionBroker = "broker01.example.test" }),
                Options.Create(new SessionRegistryOptions()),
                NullLogger<RdsSessionBackend>.Instance);
            await Assert.ThrowsAsync<RdsSessionException>(() =>
                configured.ListActiveSessionsAsync([world.Host], cancellationToken));
        }

        Assert.Equal(TimeSpan.FromSeconds(15), new SessionReconcilerOptions { IntervalSeconds = 1 }.Interval);
        Assert.Equal(TimeSpan.FromMinutes(5), new SessionReconcilerOptions { StaleAfterMinutes = 0 }.StaleAfter);
    }

    private async Task<SessionReconciliationResult> ReconcileAsync(Guid tenantId, ISessionBackend backend, DateTimeOffset now)
    {
        var tenantContext = CreateTenantContext(tenantId);
        await using var db = CreateContext(tenantContext);
        var service = new SessionReconciliationService(
            db,
            tenantContext,
            backend,
            Options.Create(new SessionRegistryOptions()),
            Options.Create(new SessionReconcilerOptions()),
            NullLogger<SessionReconciliationService>.Instance);
        return await service.ReconcileAsync(now, TestContext.Current.CancellationToken);
    }

    private async Task<Guid> AddPendingSessionAsync(LaunchWorld world, Guid userId, DateTimeOffset lastSeenAt)
    {
        await using var db = CreateContext(world.TenantId);
        var session = new RemoteSession
        {
            TenantId = world.TenantId,
            UserAccountId = userId,
            SessionHostId = world.Host.Id,
            BackendSessionId = null,
            StartedAt = lastSeenAt,
            LastSeenAt = lastSeenAt,
            SourceIp = "127.0.0.1",
            WorkstationName = "workstation-a"
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return session.Id;
    }

    private sealed class ScriptedSessionBackend(params BackendSessionSnapshot[] sessions) : ISessionBackend
    {
        public IReadOnlyList<BackendSessionSnapshot> Sessions { get; set; } = sessions;
        public Exception? Failure { get; init; }

        public Task<SessionBackendTarget?> ResolveHostAsync(RemoteApplication application, UserAccount user, CancellationToken cancellationToken)
            => Task.FromResult<SessionBackendTarget?>(null);

        public Task CancelSessionAsync(Guid sessionId, string reason, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<IReadOnlyList<BackendSessionSnapshot>> ListActiveSessionsAsync(
            IReadOnlyCollection<SessionHost> hosts,
            CancellationToken cancellationToken)
            => Failure is null ? Task.FromResult(Sessions) : Task.FromException<IReadOnlyList<BackendSessionSnapshot>>(Failure);
    }
}
