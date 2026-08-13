using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AppBridge.ControlPlane.Api.Endpoints;
using AppBridge.ControlPlane.Domain.Catalog;
using AppBridge.ControlPlane.Domain.Identity;
using AppBridge.ControlPlane.Domain.Sessions;
using AppBridge.ControlPlane.Domain.Tenancy;
using AppBridge.ControlPlane.Domain.Trail;
using AppBridge.ControlPlane.Infrastructure;
using AppBridge.ControlPlane.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AppBridge.ControlPlane.Api.Tests;

/// <summary>
/// T-504 acceptance criterion: "Repetir a chave não cria segundo lançamento nem segunda contagem
/// (ADR-0012 §3)". Runs the real Api host end to end: real login, real PostgreSQL, and a real (if
/// fake-standing-in) signing process — the whole chain <c>IAuthorizationService</c> →
/// <c>ISessionBackend</c> → <c>IRdpDescriptorBuilder</c> → <c>IRdpFileSigner</c> → <c>IAuditWriter</c>
/// T-501 through T-503 built, now with its first real caller.
/// </summary>
public sealed class LaunchEndpointTests : IAsyncLifetime
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

    private async Task<Guid> AddApplicationAsync(string alias = "dominio-contabil", ApplicationStatus status = ApplicationStatus.Published)
    {
        await using var context = NewDbContext(_tenantId);
        var application = new Application
        {
            TenantId = _tenantId,
            DisplayName = "Domínio Contábil",
            RemoteAppAlias = alias,
            HostPoolId = _hostPoolId,
            Status = status,
        };
        context.Applications.Add(application);
        await context.SaveChangesAsync();
        return application.Id;
    }

    private async Task AddOnlineHostAsync()
    {
        await using var context = NewDbContext(_tenantId);
        context.SessionHosts.Add(new SessionHost
        {
            TenantId = _tenantId,
            HostPoolId = _hostPoolId,
            Fqdn = "ab-rds01.escritorio-a.local",
            Status = SessionHostStatus.Online,
        });
        await context.SaveChangesAsync();
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
            GrantedBy = _userAccountId,
        });
        await context.SaveChangesAsync();
    }

    /// <summary>Fully wired: published application, granted permission, online host — the happy path's prerequisites.</summary>
    private async Task<Guid> SeedLaunchableApplicationAsync()
    {
        var applicationId = await AddApplicationAsync();
        await GrantPermissionAsync(applicationId);
        await AddOnlineHostAsync();
        return applicationId;
    }

    private async Task<HttpResponseMessage> PostLaunchAsync(
        string accessToken, Guid? idempotencyKey, Guid applicationId, string purpose = "user_initiated", string? workstationName = "PC-01")
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/launches");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (idempotencyKey is { } key)
        {
            request.Headers.TryAddWithoutValidation("Idempotency-Key", key.ToString());
        }

        request.Content = new StringContent(
            JsonSerializer.Serialize(new { applicationId, purpose, workstationName }), Encoding.UTF8, "application/json");
        return await _client.SendAsync(request);
    }

    [Fact]
    public async Task Request_without_a_token_is_rejected_with_401()
    {
        var response = await _client.PostAsync("/v1/launches", new StringContent("{}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Request_without_an_Idempotency_Key_header_is_400()
    {
        var applicationId = await SeedLaunchableApplicationAsync();
        var accessToken = await LoginAsAnaAsync();

        var response = await PostLaunchAsync(accessToken, idempotencyKey: null, applicationId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_fully_authorized_launch_is_granted_and_returns_a_signed_rdp_file()
    {
        var applicationId = await SeedLaunchableApplicationAsync();
        var accessToken = await LoginAsAnaAsync();

        var response = await PostLaunchAsync(accessToken, Guid.NewGuid(), applicationId);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LaunchResponse>(ReadOptions);
        Assert.NotNull(body);
        Assert.False(body!.SessionReused);
        Assert.Equal("Servidor de aplicativos", body.Host.DisplayName);

        var rdpContent = Encoding.UTF8.GetString(Convert.FromBase64String(body.RdpFile));
        Assert.Contains("remoteapplicationmode:i:1", rdpContent); // T-501's descriptor
        Assert.Contains("signed-with:test-only-thumbprint", rdpContent); // proves the fake signer actually ran

        await using var verify = NewDbContext(_tenantId);
        var launch = await verify.Launches.SingleAsync(l => l.Id == body.LaunchId);
        Assert.Equal(LaunchOutcome.Granted, launch.Outcome);
        Assert.Equal(_userAccountId, launch.UserAccountId);
    }

    [Fact]
    public async Task Repeating_the_same_key_and_body_returns_the_identical_response_and_writes_only_one_launch()
    {
        var applicationId = await SeedLaunchableApplicationAsync();
        var accessToken = await LoginAsAnaAsync();
        var idempotencyKey = Guid.NewGuid();

        var first = await PostLaunchAsync(accessToken, idempotencyKey, applicationId);
        var firstBody = await first.Content.ReadAsStringAsync();

        var second = await PostLaunchAsync(accessToken, idempotencyKey, applicationId);
        var secondBody = await second.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal(firstBody, secondBody); // byte-identical replay, not just an equivalent new grant

        await using var verify = NewDbContext(_tenantId);
        Assert.Equal(1, await verify.Launches.CountAsync()); // the literal acceptance criterion
    }

    [Fact]
    public async Task Repeating_the_same_key_with_a_different_body_is_409()
    {
        var applicationId = await SeedLaunchableApplicationAsync();
        var accessToken = await LoginAsAnaAsync();
        var idempotencyKey = Guid.NewGuid();

        await PostLaunchAsync(accessToken, idempotencyKey, applicationId, workstationName: "PC-01");
        var conflict = await PostLaunchAsync(accessToken, idempotencyKey, applicationId, workstationName: "PC-02");

        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        var problem = await conflict.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("IDEMPOTENCY_CONFLICT", problem.GetProperty("appbridgeCode").GetString());

        await using var verify = NewDbContext(_tenantId);
        Assert.Equal(1, await verify.Launches.CountAsync()); // the conflicting retry never wrote a second row
    }

    [Fact]
    public async Task An_unauthorized_application_is_denied_with_403_and_recorded_in_the_trail()
    {
        var applicationId = await AddApplicationAsync(); // never granted to Ana
        await AddOnlineHostAsync();
        var accessToken = await LoginAsAnaAsync();

        var response = await PostLaunchAsync(accessToken, Guid.NewGuid(), applicationId);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("PERMISSION_REVOKED", problem.GetProperty("appbridgeCode").GetString());

        await using var verify = NewDbContext(_tenantId);
        var launch = await verify.Launches.SingleAsync();
        Assert.Equal(LaunchOutcome.DeniedPermission, launch.Outcome);
    }

    [Fact]
    public async Task An_unknown_application_is_404_and_writes_no_launch_row()
    {
        var accessToken = await LoginAsAnaAsync();

        var response = await PostLaunchAsync(accessToken, Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("APPLICATION_NOT_FOUND", problem.GetProperty("appbridgeCode").GetString());

        await using var verify = NewDbContext(_tenantId);
        Assert.Empty(await verify.Launches.ToListAsync());
    }

    [Fact]
    public async Task An_unpublished_application_is_also_404()
    {
        var applicationId = await AddApplicationAsync(status: ApplicationStatus.Draft);
        await GrantPermissionAsync(applicationId);
        var accessToken = await LoginAsAnaAsync();

        var response = await PostLaunchAsync(accessToken, Guid.NewGuid(), applicationId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task No_online_host_is_denied_with_422_and_recorded_in_the_trail()
    {
        var applicationId = await AddApplicationAsync();
        await GrantPermissionAsync(applicationId); // authorized, but no SessionHost seeded at all
        var accessToken = await LoginAsAnaAsync();

        var response = await PostLaunchAsync(accessToken, Guid.NewGuid(), applicationId);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("APPLICATION_UNAVAILABLE", problem.GetProperty("appbridgeCode").GetString());

        await using var verify = NewDbContext(_tenantId);
        var launch = await verify.Launches.SingleAsync();
        Assert.Equal(LaunchOutcome.DeniedHostUnavailable, launch.Outcome);
    }

    [Fact]
    public async Task An_invalid_purpose_is_400_and_writes_no_launch_row()
    {
        var applicationId = await SeedLaunchableApplicationAsync();
        var accessToken = await LoginAsAnaAsync();

        var response = await PostLaunchAsync(accessToken, Guid.NewGuid(), applicationId, purpose: "not_a_real_purpose");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await using var verify = NewDbContext(_tenantId);
        Assert.Empty(await verify.Launches.ToListAsync());
    }

    [Fact]
    public async Task A_signing_failure_is_503_and_still_recorded_in_the_trail()
    {
        await using var failingFactory = ApiTestFactory.WithRdpSigner("fake-rdpsign-fail.sh");
        using var failingClient = failingFactory.CreateClient();

        var applicationId = await SeedLaunchableApplicationAsync();
        var loginResponse = await failingClient.PostAsJsonAsync(
            "/v1/auth/session", new { identityToken = "dev:oid-ana:escritorio-a.local" });
        var accessToken = (await loginResponse.Content.ReadFromJsonAsync<SessionResponse>(ReadOptions))!.AccessToken;

        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/launches");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = new StringContent(
            JsonSerializer.Serialize(new { applicationId, purpose = "user_initiated", workstationName = "PC-01" }),
            Encoding.UTF8, "application/json");

        var response = await failingClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("SIGNING_UNAVAILABLE", problem.GetProperty("appbridgeCode").GetString());

        await using var verify = NewDbContext(_tenantId);
        var launch = await verify.Launches.SingleAsync();
        Assert.Equal(LaunchOutcome.ErrorSigning, launch.Outcome);
    }

    [Fact]
    public async Task Purpose_prelaunch_is_recorded_on_the_launch_row()
    {
        var applicationId = await SeedLaunchableApplicationAsync();
        var accessToken = await LoginAsAnaAsync();

        var response = await PostLaunchAsync(accessToken, Guid.NewGuid(), applicationId, purpose: "prelaunch");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await using var verify = NewDbContext(_tenantId);
        var launch = await verify.Launches.SingleAsync();
        Assert.Equal(LaunchPurpose.Prelaunch, launch.Purpose);
    }
}
