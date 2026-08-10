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
/// T-402 acceptance criterion: "Aplicativo não autorizado não aparece (RF-011)". Runs the real Api
/// host end to end — a real login through <c>POST /v1/auth/session</c> to get a genuine access
/// token, then <c>GET /v1/applications</c> with it — so <see cref="TenantResolutionMiddleware"/> and
/// the <c>[Authorize]</c> gate are exercised for real, not bypassed by constructing a handler call
/// directly.
/// </summary>
public sealed class CatalogEndpointTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };

    private string _connectionString = null!;
    private ApiTestFactory _factory = null!;
    private HttpClient _client = null!;
    private Guid _tenantId;
    private Guid _userAccountId;
    private Guid _hostPoolId;

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

    private async Task<Application> AddApplicationAsync(
        string displayName, string alias, ApplicationStatus status = ApplicationStatus.Published)
    {
        await using var context = NewDbContext(_tenantId);
        var application = new Application
        {
            TenantId = _tenantId,
            DisplayName = displayName,
            RemoteAppAlias = alias,
            HostPoolId = _hostPoolId,
            Status = status,
        };
        context.Applications.Add(application);
        await context.SaveChangesAsync();
        return application;
    }

    private async Task GrantPermissionAsync(Guid applicationId)
    {
        await using var context = NewDbContext(_tenantId);
        var group = new Group { TenantId = _tenantId, Name = "Contabilidade" };
        context.Groups.Add(group);
        await context.SaveChangesAsync();

        context.UserGroupMemberships.Add(new UserGroupMembership
        {
            TenantId = _tenantId,
            UserAccountId = _userAccountId,
            GroupId = group.Id,
        });
        context.ApplicationPermissions.Add(new ApplicationPermission
        {
            TenantId = _tenantId,
            ApplicationId = applicationId,
            GroupId = group.Id,
            EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1),
            GrantedBy = _userAccountId, // no separate admin fixture needed for this test's purpose
        });
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task Request_without_a_token_is_rejected_with_401()
    {
        var response = await _client.GetAsync("/v1/applications");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Authorized_and_published_applications_appear_in_the_catalog()
    {
        var application = await AddApplicationAsync("Domínio Contábil", "dominio-contabil");
        await GrantPermissionAsync(application.Id);
        var accessToken = await LoginAsAnaAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/applications");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CatalogResponse>(ReadOptions);
        var item = Assert.Single(body!.Items);
        Assert.Equal(application.Id, item.Id);
        Assert.Equal("Domínio Contábil", item.DisplayName);
        Assert.Equal($"/v1/applications/{application.Id}/icon", item.IconUrl);
        Assert.Equal($"appbridge://launch/{application.Id}", item.ProtocolUri);
        Assert.True(item.Available);
    }

    [Fact]
    public async Task An_application_the_user_is_not_authorized_for_does_not_appear()
    {
        await AddApplicationAsync("Alterdata", "alterdata"); // never granted to Ana
        var accessToken = await LoginAsAnaAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/applications");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CatalogResponse>(ReadOptions);
        Assert.Empty(body!.Items);
    }

    [Fact]
    public async Task An_authorized_but_unpublished_application_does_not_appear()
    {
        var draft = await AddApplicationAsync("Em preparação", "em-preparacao", ApplicationStatus.Draft);
        await GrantPermissionAsync(draft.Id);
        var accessToken = await LoginAsAnaAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/applications");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CatalogResponse>(ReadOptions);
        Assert.Empty(body!.Items);
    }

    [Fact]
    public async Task A_token_from_one_tenant_never_sees_another_tenants_application()
    {
        var ownApplication = await AddApplicationAsync("Domínio Contábil", "dominio-contabil");
        await GrantPermissionAsync(ownApplication.Id);
        var accessToken = await LoginAsAnaAsync();

        // A second tenant, with its own application and its own permission — never granted to Ana,
        // and not even reachable through her tenant's data at all.
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

            context.Applications.Add(new Application
            {
                TenantId = otherTenantId,
                DisplayName = "Sistema do Escritório B",
                RemoteAppAlias = "sistema-b",
                HostPoolId = otherHostPool.Id,
                Status = ApplicationStatus.Published,
            });
            await context.SaveChangesAsync();
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/applications");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _client.SendAsync(request);

        var body = await response.Content.ReadFromJsonAsync<CatalogResponse>(ReadOptions);
        var item = Assert.Single(body!.Items);
        Assert.Equal(ownApplication.Id, item.Id); // only her own tenant's authorized application
    }
}
