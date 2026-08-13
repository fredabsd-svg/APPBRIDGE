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
/// T-601 acceptance criterion: "Segundo aplicativo reutiliza a sessão" (RF-024). Real PostgreSQL,
/// no fake — <see cref="SessionRegistry"/> never talks to RDS, it only reads/writes the
/// <c>session</c> table this codebase already owns (T-204), same category as
/// <see cref="RdsSessionBackendTests"/>'s own reasoning for T-503.
/// </summary>
public sealed class SessionRegistryTests : IAsyncLifetime
{
    private string _connectionString = null!;
    private Guid _tenantId;
    private Guid _hostPoolId;
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
        _hostPoolId = hostPool.Id;

        var user = new UserAccount
        {
            TenantId = _tenantId,
            ExternalSubject = "oid-ana",
            Upn = "ana@escritorio-a.local",
            AdObjectSid = "S-1-5-21-0-0-0-1001",
            DisplayName = "Ana Souza",
        };
        setup.UserAccounts.Add(user);
        await setup.SaveChangesAsync();
        _userAccountId = user.Id;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private AppBridgeDbContext NewContext() => new(
        new DbContextOptionsBuilder<AppBridgeDbContext>().UseNpgsql(_connectionString).Options,
        new TenantContext { TenantId = _tenantId });

    private async Task<SessionHost> AddHostAsync(string fqdn)
    {
        await using var context = NewContext();
        var host = new SessionHost { TenantId = _tenantId, HostPoolId = _hostPoolId, Fqdn = fqdn, Status = SessionHostStatus.Online };
        context.SessionHosts.Add(host);
        await context.SaveChangesAsync();
        return host;
    }

    [Fact]
    public async Task First_registration_for_a_user_on_a_host_creates_a_new_session()
    {
        var host = await AddHostAsync("ab-rds01.escritorio-a.local");

        await using var context = NewContext();
        var registration = await new SessionRegistry(context)
            .RegisterAsync(_tenantId, _userAccountId, host, sourceIp: "10.0.0.5", workstationName: "PC-01");
        await context.SaveChangesAsync();

        Assert.False(registration.Reused);

        await using var verify = NewContext();
        var session = await verify.Sessions.SingleAsync(s => s.Id == registration.SessionId);
        Assert.Equal(_userAccountId, session.UserAccountId);
        Assert.Equal(host.Id, session.SessionHostId);
        Assert.Null(session.EndedAt);
        Assert.Equal("10.0.0.5", session.SourceIp);
        Assert.Equal("PC-01", session.WorkstationName);
        Assert.False(string.IsNullOrWhiteSpace(session.BackendSessionId));
    }

    [Fact]
    public async Task A_second_registration_for_the_same_user_and_host_reuses_the_session()
    {
        var host = await AddHostAsync("ab-rds01.escritorio-a.local");

        Guid firstSessionId;
        await using (var context = NewContext())
        {
            var first = await new SessionRegistry(context).RegisterAsync(_tenantId, _userAccountId, host, null, "PC-01");
            await context.SaveChangesAsync();
            firstSessionId = first.SessionId;
        }

        await using var context2 = NewContext();
        var second = await new SessionRegistry(context2).RegisterAsync(_tenantId, _userAccountId, host, null, "PC-01");
        await context2.SaveChangesAsync();

        Assert.True(second.Reused);
        Assert.Equal(firstSessionId, second.SessionId);

        await using var verify = NewContext();
        Assert.Single(await verify.Sessions.ToListAsync()); // no second row was created
    }

    [Fact]
    public async Task Reusing_a_session_bumps_LastSeenAt()
    {
        var host = await AddHostAsync("ab-rds01.escritorio-a.local");

        Guid sessionId;
        DateTimeOffset firstLastSeenAt;
        await using (var context = NewContext())
        {
            var first = await new SessionRegistry(context).RegisterAsync(_tenantId, _userAccountId, host, null, null);
            await context.SaveChangesAsync();
            sessionId = first.SessionId;
            firstLastSeenAt = (await context.Sessions.SingleAsync(s => s.Id == sessionId)).LastSeenAt;
        }

        await Task.Delay(TimeSpan.FromMilliseconds(50)); // guarantee a distinguishable timestamp

        await using (var context = NewContext())
        {
            await new SessionRegistry(context).RegisterAsync(_tenantId, _userAccountId, host, null, null);
            await context.SaveChangesAsync();
        }

        await using var verify = NewContext();
        var session = await verify.Sessions.SingleAsync(s => s.Id == sessionId);
        Assert.True(session.LastSeenAt > firstLastSeenAt);
    }

    [Fact]
    public async Task A_different_host_does_not_reuse_the_session()
    {
        var hostA = await AddHostAsync("a.escritorio-a.local");
        var hostB = await AddHostAsync("b.escritorio-a.local");

        await using (var context = NewContext())
        {
            await new SessionRegistry(context).RegisterAsync(_tenantId, _userAccountId, hostA, null, null);
            await context.SaveChangesAsync();
        }

        await using var context2 = NewContext();
        var registration = await new SessionRegistry(context2).RegisterAsync(_tenantId, _userAccountId, hostB, null, null);
        await context2.SaveChangesAsync();

        Assert.False(registration.Reused);

        await using var verify = NewContext();
        Assert.Equal(2, await verify.Sessions.CountAsync());
    }

    [Fact]
    public async Task A_different_user_on_the_same_host_does_not_reuse_the_session()
    {
        var host = await AddHostAsync("ab-rds01.escritorio-a.local");

        await using (var context = NewContext())
        {
            await new SessionRegistry(context).RegisterAsync(_tenantId, _userAccountId, host, null, null);
            await context.SaveChangesAsync();
        }

        var otherUser = new UserAccount
        {
            TenantId = _tenantId,
            ExternalSubject = "oid-carlos",
            Upn = "carlos@escritorio-a.local",
            AdObjectSid = "S-1-5-21-0-0-0-1002",
            DisplayName = "Carlos Lima",
        };
        await using (var context = NewContext())
        {
            context.UserAccounts.Add(otherUser);
            await context.SaveChangesAsync();
        }

        await using var context2 = NewContext();
        var registration = await new SessionRegistry(context2).RegisterAsync(_tenantId, otherUser.Id, host, null, null);
        await context2.SaveChangesAsync();

        Assert.False(registration.Reused);

        await using var verify = NewContext();
        Assert.Equal(2, await verify.Sessions.CountAsync());
    }

    [Fact]
    public async Task An_ended_session_is_not_reused_a_new_one_is_created()
    {
        var host = await AddHostAsync("ab-rds01.escritorio-a.local");

        Guid firstSessionId;
        await using (var context = NewContext())
        {
            var first = await new SessionRegistry(context).RegisterAsync(_tenantId, _userAccountId, host, null, null);
            await context.SaveChangesAsync();
            firstSessionId = first.SessionId;
        }

        await using (var context = NewContext())
        {
            var session = await context.Sessions.SingleAsync(s => s.Id == firstSessionId);
            session.EndedAt = DateTimeOffset.UtcNow;
            session.EndReason = SessionEndReason.Logoff;
            await context.SaveChangesAsync();
        }

        await using var context2 = NewContext();
        var registration = await new SessionRegistry(context2).RegisterAsync(_tenantId, _userAccountId, host, null, null);
        await context2.SaveChangesAsync();

        Assert.False(registration.Reused);
        Assert.NotEqual(firstSessionId, registration.SessionId);
    }
}
