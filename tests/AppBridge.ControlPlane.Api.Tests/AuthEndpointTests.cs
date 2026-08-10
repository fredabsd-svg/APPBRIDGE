using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AppBridge.ControlPlane.Api.Endpoints;
using AppBridge.ControlPlane.Domain.Identity;
using AppBridge.ControlPlane.Domain.Tenancy;
using AppBridge.ControlPlane.Domain.Trail;
using AppBridge.ControlPlane.Infrastructure;
using AppBridge.ControlPlane.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace AppBridge.ControlPlane.Api.Tests;

/// <summary>
/// T-301 acceptance criterion: "Login gera access_event; falha de trilha devolve
/// 503 AUDIT_UNAVAILABLE". Runs the real Api host (<see cref="WebApplicationFactory{TEntryPoint}"/>)
/// against a real PostgreSQL test database — the same approach as the Infrastructure test suite
/// (no Docker/Testcontainers, docs/SETUP-DEV.md), but exercised over real HTTP through the pipeline
/// Program.cs actually wires (CorrelationIdMiddleware, JwtBearer, the endpoint itself), not by
/// calling the handler method directly.
/// </summary>
public sealed class AuthEndpointTests : IAsyncLifetime
{
    private string _connectionString = null!;
    private ApiTestFactory _factory = null!;
    private HttpClient _client = null!;
    private Guid _tenantId;
    private Guid _userAccountId;

    public async Task InitializeAsync()
    {
        _connectionString = Environment.GetEnvironmentVariable("APPBRIDGE_TEST_DB_CONNECTION")
            ?? throw new InvalidOperationException(
                "Set APPBRIDGE_TEST_DB_CONNECTION before running the Api tests (see docs/SETUP-DEV.md).");

        _factory = new ApiTestFactory();
        _client = _factory.CreateClient();

        await using var context = NewDbContext();
        var migrator = context.GetInfrastructure().GetRequiredService<IMigrator>();
        await migrator.MigrateAsync(Migration.InitialDatabase); // clean slate
        await migrator.MigrateAsync();

        var tenant = new Tenant { Name = "Escritório A", Slug = "escritorio-a", AdDomain = "escritorio-a.local" };
        context.Tenants.Add(tenant);
        await context.SaveChangesAsync();
        _tenantId = tenant.Id;

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

    // Scoped to _tenantId: every setup/verification query here is a legitimate within-tenant read.
    // An unscoped context would have T-203's tenant filter blind it to rows that are really there.
    private AppBridgeDbContext NewDbContext() => new(
        new DbContextOptionsBuilder<AppBridgeDbContext>().UseNpgsql(_connectionString).Options,
        new TenantContext { TenantId = _tenantId });

    [Fact]
    public async Task Successful_login_returns_tokens_and_writes_an_access_event()
    {
        var response = await _client.PostAsJsonAsync(
            "/v1/auth/session", new { identityToken = "dev:oid-ana:escritorio-a.local", workstationName = "PC-01" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(body.RefreshToken));
        Assert.Equal("Ana Souza", body.User.DisplayName);
        Assert.Equal(_tenantId, body.User.Tenant.Id);

        await using var verify = NewDbContext();
        var accessEvent = await verify.AccessEvents.SingleAsync(e => e.UserAccountId == _userAccountId);
        Assert.Equal(AccessEventResult.Success, accessEvent.Result);
        Assert.Equal("PC-01", accessEvent.WorkstationName);

        var refreshTokenRow = await verify.RefreshTokens.SingleAsync(r => r.UserAccountId == _userAccountId);
        Assert.Null(refreshTokenRow.RevokedAt);

        var user = await verify.UserAccounts.SingleAsync(u => u.Id == _userAccountId);
        Assert.NotNull(user.LastLoginAt);
    }

    [Fact]
    public async Task Invalid_identity_token_is_rejected_and_not_attributed_to_any_tenant()
    {
        var response = await _client.PostAsJsonAsync("/v1/auth/session", new { identityToken = "garbage" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("INVALID_IDENTITY_TOKEN", problem.GetProperty("appbridgeCode").GetString());

        // No tenant could be resolved for "garbage" — nothing to attribute an access_event to.
        await using var verify = NewDbContext();
        Assert.Empty(await verify.AccessEvents.ToListAsync());
    }

    [Fact]
    public async Task Unknown_user_within_a_known_tenant_is_recorded_with_a_null_user_account_id()
    {
        var response = await _client.PostAsJsonAsync(
            "/v1/auth/session", new { identityToken = "dev:oid-does-not-exist:escritorio-a.local" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        await using var verify = NewDbContext();
        var accessEvent = await verify.AccessEvents.SingleAsync(e => e.TenantId == _tenantId);
        Assert.Null(accessEvent.UserAccountId);
        Assert.Equal(AccessEventResult.Failure, accessEvent.Result);
    }

    [Fact]
    public async Task Disabled_user_is_rejected_with_403_USER_DISABLED()
    {
        await using (var setup = NewDbContext())
        {
            var user = await setup.UserAccounts.SingleAsync(u => u.Id == _userAccountId);
            user.Status = UserAccountStatus.Disabled;
            await setup.SaveChangesAsync();
        }

        var response = await _client.PostAsJsonAsync("/v1/auth/session", new { identityToken = "dev:oid-ana:escritorio-a.local" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("USER_DISABLED", problem.GetProperty("appbridgeCode").GetString());
    }

    [Fact]
    public async Task Suspended_tenant_is_rejected_with_403_TENANT_SUSPENDED()
    {
        await using (var setup = NewDbContext())
        {
            var tenant = await setup.Tenants.SingleAsync(t => t.Id == _tenantId);
            tenant.Status = TenantStatus.Suspended;
            await setup.SaveChangesAsync();
        }

        var response = await _client.PostAsJsonAsync("/v1/auth/session", new { identityToken = "dev:oid-ana:escritorio-a.local" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("TENANT_SUSPENDED", problem.GetProperty("appbridgeCode").GetString());
    }

    [Fact]
    public async Task Simulated_audit_write_failure_denies_login_with_503_AUDIT_UNAVAILABLE()
    {
        // A dedicated factory: only this test's DbContext registration writes through a
        // SaveChangesInterceptor that fails exactly where EF Core would issue the SQL — the same
        // simulation technique T-205's AuditWriterTests uses, now proven at the HTTP boundary.
        await using var throwingFactory = new ApiTestFactory().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<AppBridgeDbContext>>();
                services.RemoveAll<AppBridgeDbContext>();
                services.AddDbContext<AppBridgeDbContext>(options =>
                    options.UseNpgsql(_connectionString).AddInterceptors(new ThrowingSaveChangesInterceptor()));
            });
        });
        using var throwingClient = throwingFactory.CreateClient();

        var response = await throwingClient.PostAsJsonAsync(
            "/v1/auth/session", new { identityToken = "dev:oid-ana:escritorio-a.local" });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("AUDIT_UNAVAILABLE", problem.GetProperty("appbridgeCode").GetString());

        // The reads that precede the write (tenant/user lookup) still succeeded — only the write
        // failed — so nothing about this proves reads were blocked too; it proves specifically that
        // a write failure denies the login, per T-301's literal acceptance criterion.
        await using var verify = NewDbContext();
        Assert.Empty(await verify.AccessEvents.ToListAsync());
    }

    private sealed class ThrowingSaveChangesInterceptor : SaveChangesInterceptor
    {
        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
            => throw new TimeoutException("Simulated database write failure (T-301 test double).");

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
            => throw new TimeoutException("Simulated database write failure (T-301 test double).");
    }
}
