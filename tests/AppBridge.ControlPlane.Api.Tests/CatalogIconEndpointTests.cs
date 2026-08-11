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
/// T-404 acceptance criterion: "Serve PNG com cache; resolve PD-03". <c>ApiTestFactory</c> points
/// <c>APPBRIDGE_ICON_STORAGE_PATH</c> at a self-contained temp directory carrying
/// <c>dominio-contabil.png</c>/<c>alterdata.png</c> — real bytes on real disk, not a mocked
/// <c>IIconStorage</c>.
/// </summary>
public sealed class CatalogIconEndpointTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };

    private string _connectionString = null!;
    private ApiTestFactory _factory = null!;
    private HttpClient _client = null!;
    private Guid _tenantId;
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

    private async Task<Guid> AddApplicationAsync(string alias, string? iconRef)
    {
        await using var context = NewDbContext(_tenantId);
        var application = new Application
        {
            TenantId = _tenantId,
            DisplayName = alias,
            RemoteAppAlias = alias,
            HostPoolId = _hostPoolId,
            Status = ApplicationStatus.Published,
            IconRef = iconRef,
        };
        context.Applications.Add(application);
        await context.SaveChangesAsync();
        return application.Id;
    }

    private async Task<HttpResponseMessage> GetIconAsync(Guid applicationId, string accessToken, string? ifNoneMatch = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/v1/applications/{applicationId}/icon");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (ifNoneMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-None-Match", ifNoneMatch);
        }

        return await _client.SendAsync(request);
    }

    [Fact]
    public async Task Request_without_a_token_is_rejected_with_401()
    {
        var response = await _client.GetAsync($"/v1/applications/{Guid.NewGuid()}/icon");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Serves_the_PNG_bytes_with_ETag_and_a_long_Cache_Control()
    {
        var applicationId = await AddApplicationAsync("dominio-contabil", "dominio-contabil.png");
        var accessToken = await LoginAsAnaAsync();

        var response = await GetIconAsync(applicationId, accessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        Assert.False(string.IsNullOrWhiteSpace(response.Headers.ETag?.Tag));
        Assert.NotNull(response.Headers.CacheControl);
        Assert.True(response.Headers.CacheControl!.Public);
        Assert.True(response.Headers.CacheControl.MaxAge is { } maxAge && maxAge >= TimeSpan.FromDays(1));

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.NotEmpty(bytes);
    }

    [Fact]
    public async Task Second_request_with_the_same_ETag_returns_304_with_no_body()
    {
        var applicationId = await AddApplicationAsync("dominio-contabil", "dominio-contabil.png");
        var accessToken = await LoginAsAnaAsync();

        var first = await GetIconAsync(applicationId, accessToken);
        var etag = first.Headers.ETag?.Tag;

        var second = await GetIconAsync(applicationId, accessToken, ifNoneMatch: etag);

        Assert.Equal(HttpStatusCode.NotModified, second.StatusCode);
        Assert.Equal(etag, second.Headers.ETag?.Tag);
        Assert.Equal(0, second.Content.Headers.ContentLength ?? 0);
    }

    [Fact]
    public async Task An_application_without_an_icon_reference_is_404()
    {
        var applicationId = await AddApplicationAsync("sem-icone", iconRef: null);
        var accessToken = await LoginAsAnaAsync();

        var response = await GetIconAsync(applicationId, accessToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task An_icon_reference_that_does_not_resolve_to_a_file_is_404()
    {
        var applicationId = await AddApplicationAsync("icone-inexistente", "does-not-exist.png");
        var accessToken = await LoginAsAnaAsync();

        var response = await GetIconAsync(applicationId, accessToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task An_unknown_application_id_is_404()
    {
        var accessToken = await LoginAsAnaAsync();

        var response = await GetIconAsync(Guid.NewGuid(), accessToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_token_from_one_tenant_cannot_read_another_tenants_icon()
    {
        Guid otherTenantId;
        Guid otherApplicationId;
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

            var otherApplication = new Application
            {
                TenantId = otherTenantId,
                DisplayName = "Sistema do Escritório B",
                RemoteAppAlias = "sistema-b",
                HostPoolId = otherHostPool.Id,
                Status = ApplicationStatus.Published,
                IconRef = "alterdata.png",
            };
            context.Applications.Add(otherApplication);
            await context.SaveChangesAsync();
            otherApplicationId = otherApplication.Id;
        }

        var accessToken = await LoginAsAnaAsync(); // logged into tenant A

        var response = await GetIconAsync(otherApplicationId, accessToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
