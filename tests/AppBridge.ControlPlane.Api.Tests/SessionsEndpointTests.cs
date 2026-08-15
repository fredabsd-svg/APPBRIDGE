using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AppBridge.ControlPlane.Api.Endpoints;
using AppBridge.ControlPlane.Domain.Catalog;
using AppBridge.ControlPlane.Domain.Identity;
using AppBridge.ControlPlane.Domain.Sessions;
using AppBridge.ControlPlane.Domain.Tenancy;
using AppBridge.ControlPlane.Infrastructure;
using AppBridge.ControlPlane.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AppBridge.ControlPlane.Api.Tests;

/// <summary>
/// T-603 acceptance criterion: "Launcher exibe sessões ativas" (API.md §4, RF-024, RF-027).
/// <c>GET /v1/sessions/me</c> is the last E-06 task — the first time any active <see cref="Session"/>
/// T-601/T-602 manage becomes observable through the API instead of only through <c>psql</c>.
/// </summary>
public sealed class SessionsEndpointTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };

    private string _connectionString = null!;
    private ApiTestFactory _factory = null!;
    private HttpClient _client = null!;
    private Guid _tenantId;
    private Guid _userAccountId;
    private Guid _hostPoolId;
    private Guid _hostId;

    public async Task InitializeAsync()
    {
        _connectionString = Environment.GetEnvironmentVariable("APPBRIDGE_TEST_DB_CONNECTION")
            ?? throw new InvalidOperationException(
                "Set APPBRIDGE_TEST_DB_CONNECTION before running the Api tests (see docs/SETUP-DEV.md).");

        _factory = new ApiTestFactory();
        _client = _factory.CreateClient();

        await using var context = NewDbContext(Guid.Empty);
        var migrator = context.GetInfrastructure().GetRequiredService<IMigrator>();
        await migrator.MigrateAsync(Migration.InitialDatabase); // clean slate
        await migrator.MigrateAsync();

        var tenant = new Tenant { Name = "Escritório A", Slug = "escritorio-a", AdDomain = "escritorio-a.local" };
        context.Tenants.Add(tenant);
        await context.SaveChangesAsync();
        _tenantId = tenant.Id;

        var hostPool = new HostPool { TenantId = _tenantId, Name = "Pool A" };
        context.HostPools.Add(hostPool);
        await context.SaveChangesAsync();
        _hostPoolId = hostPool.Id;

        var host = new SessionHost { TenantId = _tenantId, HostPoolId = _hostPoolId, Fqdn = "ab-rds01.escritorio-a.local", Status = SessionHostStatus.Online };
        context.SessionHosts.Add(host);
        await context.SaveChangesAsync();
        _hostId = host.Id;

        var user = new UserAccount
        {
            TenantId = _tenantId,
            ExternalSubject = "oid-ana",
            Upn = "ana@escritorio-a.local",
            AdObjectSid = "S-1-5-21-0-0-0-1001",
            DisplayName = "Ana Souza",
        };
        context.UserAccounts.Add(user);
        await context.SaveChangesAsync();
        _userAccountId = user.Id;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    private AppBridgeDbContext NewDbContext(Guid tenantId) => new(
        new DbContextOptionsBuilder<AppBridgeDbContext>().UseNpgsql(_connectionString).Options,
        new TenantContext { TenantId = tenantId });

    private async Task<string> LoginAsAnaAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/v1/auth/session", new { identityToken = "dev:oid-ana:escritorio-a.local" });
        var body = await response.Content.ReadFromJsonAsync<SessionResponse>(ReadOptions);
        return body!.AccessToken;
    }

    private async Task<Guid> SeedSessionAsync(Guid userAccountId, DateTimeOffset? endedAt = null)
    {
        await using var context = NewDbContext(_tenantId);
        var session = new Session
        {
            TenantId = _tenantId,
            UserAccountId = userAccountId,
            SessionHostId = _hostId,
            BackendSessionId = $"pending:{Guid.NewGuid()}",
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            LastSeenAt = DateTimeOffset.UtcNow,
            EndedAt = endedAt,
            EndReason = endedAt is null ? null : SessionEndReason.Logoff,
        };
        context.Sessions.Add(session);
        await context.SaveChangesAsync();
        return session.Id;
    }

    private async Task<HttpResponseMessage> GetMySessionsAsync(string? accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/sessions/me");
        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return await _client.SendAsync(request);
    }

    [Fact]
    public async Task Request_without_a_token_is_rejected_with_401_SESSION_EXPIRED()
    {
        var response = await GetMySessionsAsync(accessToken: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("SESSION_EXPIRED", problem.GetProperty("appbridgeCode").GetString());
    }

    [Fact]
    public async Task No_active_sessions_returns_an_empty_list()
    {
        var accessToken = await LoginAsAnaAsync();

        var response = await GetMySessionsAsync(accessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SessionsResponse>(ReadOptions);
        Assert.NotNull(body);
        Assert.Empty(body!.Items);
    }

    [Fact]
    public async Task An_active_session_appears_with_its_start_and_last_seen_timestamps()
    {
        var sessionId = await SeedSessionAsync(_userAccountId);
        var accessToken = await LoginAsAnaAsync();

        var response = await GetMySessionsAsync(accessToken);

        var body = await response.Content.ReadFromJsonAsync<SessionsResponse>(ReadOptions);
        var item = Assert.Single(body!.Items);
        Assert.Equal(sessionId, item.Id);
        Assert.Equal("Servidor de aplicativos", item.Host.DisplayName); // RNF-043 — no FQDN leaked
        Assert.True(item.LastSeenAt >= item.StartedAt);
    }

    [Fact]
    public async Task An_ended_session_does_not_appear()
    {
        await SeedSessionAsync(_userAccountId, endedAt: DateTimeOffset.UtcNow);
        var accessToken = await LoginAsAnaAsync();

        var response = await GetMySessionsAsync(accessToken);

        var body = await response.Content.ReadFromJsonAsync<SessionsResponse>(ReadOptions);
        Assert.Empty(body!.Items);
    }

    [Fact]
    public async Task Another_users_active_session_does_not_appear()
    {
        await using (var context = NewDbContext(_tenantId))
        {
            var otherUser = new UserAccount
            {
                TenantId = _tenantId,
                ExternalSubject = "oid-carlos",
                Upn = "carlos@escritorio-a.local",
                AdObjectSid = "S-1-5-21-0-0-0-1002",
                DisplayName = "Carlos Lima",
            };
            context.UserAccounts.Add(otherUser);
            await context.SaveChangesAsync();
            await SeedSessionAsync(otherUser.Id);
        }

        var accessToken = await LoginAsAnaAsync();
        var response = await GetMySessionsAsync(accessToken);

        var body = await response.Content.ReadFromJsonAsync<SessionsResponse>(ReadOptions);
        Assert.Empty(body!.Items);
    }

    [Fact]
    public async Task Another_tenants_active_session_does_not_appear()
    {
        Guid otherTenantId;
        await using (var context = NewDbContext(Guid.Empty))
        {
            var otherTenant = new Tenant { Name = "Escritório B", Slug = "escritorio-b", AdDomain = "escritorio-b.local" };
            context.Tenants.Add(otherTenant);
            await context.SaveChangesAsync();
            otherTenantId = otherTenant.Id;
        }

        await using (var context = NewDbContext(otherTenantId))
        {
            var otherHostPool = new HostPool { TenantId = otherTenantId, Name = "Pool B" };
            context.HostPools.Add(otherHostPool);
            await context.SaveChangesAsync();

            var otherHost = new SessionHost { TenantId = otherTenantId, HostPoolId = otherHostPool.Id, Fqdn = "b.escritorio-b.local", Status = SessionHostStatus.Online };
            context.SessionHosts.Add(otherHost);
            await context.SaveChangesAsync();

            // Same UserAccount.Id value as Ana's would be impossible (Guid v7, generated) — reusing
            // her actual id would violate the composite FK to the other tenant's own UserAccount
            // table, so this session simply cannot belong to another tenant's row that resolves to
            // her — the scenario that matters here is a session under a *different* tenant existing
            // at all, not colliding identities.
            var otherUser = new UserAccount
            {
                TenantId = otherTenantId,
                ExternalSubject = "oid-ana", // same external subject, different tenant — must not leak across
                Upn = "ana@escritorio-b.local",
                AdObjectSid = "S-1-5-21-0-0-0-9001",
                DisplayName = "Ana (Escritório B)",
            };
            context.UserAccounts.Add(otherUser);
            await context.SaveChangesAsync();

            context.Sessions.Add(new Session
            {
                TenantId = otherTenantId,
                UserAccountId = otherUser.Id,
                SessionHostId = otherHost.Id,
                BackendSessionId = "pending:other-tenant",
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
                LastSeenAt = DateTimeOffset.UtcNow,
            });
            await context.SaveChangesAsync();
        }

        var accessToken = await LoginAsAnaAsync(); // logged into tenant A
        var response = await GetMySessionsAsync(accessToken);

        var body = await response.Content.ReadFromJsonAsync<SessionsResponse>(ReadOptions);
        Assert.Empty(body!.Items);
    }

    [Fact]
    public async Task A_real_launch_makes_its_session_visible_end_to_end()
    {
        await using (var context = NewDbContext(_tenantId))
        {
            var application = new Application
            {
                TenantId = _tenantId,
                DisplayName = "Domínio Contábil",
                RemoteAppAlias = "dominio-contabil",
                HostPoolId = _hostPoolId,
                Status = ApplicationStatus.Published,
            };
            context.Applications.Add(application);
            await context.SaveChangesAsync();

            var group = new Group { TenantId = _tenantId, Name = "Contabilidade" };
            context.Groups.Add(group);
            await context.SaveChangesAsync();

            context.UserGroupMemberships.Add(new UserGroupMembership { TenantId = _tenantId, UserAccountId = _userAccountId, GroupId = group.Id });
            context.ApplicationPermissions.Add(new ApplicationPermission
            {
                TenantId = _tenantId,
                ApplicationId = application.Id,
                GroupId = group.Id,
                EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1),
                GrantedBy = _userAccountId,
            });
            await context.SaveChangesAsync();

            var accessToken = await LoginAsAnaAsync();
            using var launchRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/launches");
            launchRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            launchRequest.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.NewGuid().ToString());
            launchRequest.Content = JsonContent.Create(new { applicationId = application.Id, purpose = "user_initiated", workstationName = "PC-01" });
            var launchResponse = await _client.SendAsync(launchRequest);
            Assert.Equal(HttpStatusCode.Created, launchResponse.StatusCode);

            var sessionsResponse = await GetMySessionsAsync(accessToken);
            var body = await sessionsResponse.Content.ReadFromJsonAsync<SessionsResponse>(ReadOptions);
            Assert.Single(body!.Items);
        }
    }
}
