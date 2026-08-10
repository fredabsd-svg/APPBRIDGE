using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AppBridge.ControlPlane.Api.Endpoints;
using AppBridge.ControlPlane.Domain.Identity;
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
/// T-303 acceptance criterion: "Token renova sem login; logout invalida" (RF-004, RF-006). Each
/// test logs in through the real <c>POST /v1/auth/session</c> endpoint first to obtain a genuine
/// refresh token — end-to-end, not a token hand-assembled in the test — then exercises
/// <c>/refresh</c> and/or <c>/logout</c> against it. Same real-host, real-PostgreSQL approach as
/// <see cref="AuthEndpointTests"/>.
/// </summary>
public sealed class RefreshLogoutEndpointTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };

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

    private AppBridgeDbContext NewDbContext() => new(
        new DbContextOptionsBuilder<AppBridgeDbContext>().UseNpgsql(_connectionString).Options,
        new TenantContext { TenantId = _tenantId });

    private async Task<string> LoginAndGetRefreshTokenAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/v1/auth/session", new { identityToken = "dev:oid-ana:escritorio-a.local" });
        var body = await response.Content.ReadFromJsonAsync<SessionResponse>(ReadOptions);
        return body!.RefreshToken;
    }

    [Fact]
    public async Task Refresh_with_a_valid_token_issues_a_new_pair_and_revokes_the_presented_one()
    {
        var originalRefreshToken = await LoginAndGetRefreshTokenAsync();

        var response = await _client.PostAsJsonAsync("/v1/auth/refresh", new { refreshToken = originalRefreshToken });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SessionResponse>(ReadOptions);
        Assert.NotNull(body);
        Assert.NotEqual(originalRefreshToken, body!.RefreshToken);
        Assert.Equal("Ana Souza", body.User.DisplayName);

        await using var verify = NewDbContext();
        var tokens = await verify.RefreshTokens.OrderBy(t => t.ExpiresAt).ToListAsync();
        Assert.Equal(2, tokens.Count);
        Assert.NotNull(tokens[0].RevokedAt); // the original, rotated away
        Assert.Null(tokens[1].RevokedAt); // the newly issued one
    }

    [Fact]
    public async Task Reusing_an_already_rotated_refresh_token_is_rejected_with_401_REFRESH_EXPIRED()
    {
        var originalRefreshToken = await LoginAndGetRefreshTokenAsync();
        await _client.PostAsJsonAsync("/v1/auth/refresh", new { refreshToken = originalRefreshToken }); // rotates it away

        var reuse = await _client.PostAsJsonAsync("/v1/auth/refresh", new { refreshToken = originalRefreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
        var problem = await reuse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("REFRESH_EXPIRED", problem.GetProperty("appbridgeCode").GetString());
    }

    [Fact]
    public async Task Refresh_with_an_unknown_token_is_rejected_with_401_REFRESH_EXPIRED()
    {
        var response = await _client.PostAsJsonAsync("/v1/auth/refresh", new { refreshToken = "not-a-real-token" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("REFRESH_EXPIRED", problem.GetProperty("appbridgeCode").GetString());
        Assert.Equal("/v1/auth/refresh", problem.GetProperty("instance").GetString());
    }

    [Fact]
    public async Task Refresh_for_a_disabled_user_is_rejected_with_403_USER_DISABLED()
    {
        var refreshToken = await LoginAndGetRefreshTokenAsync();
        await using (var setup = NewDbContext())
        {
            var user = await setup.UserAccounts.SingleAsync(u => u.Id == _userAccountId);
            user.Status = UserAccountStatus.Disabled;
            await setup.SaveChangesAsync();
        }

        var response = await _client.PostAsJsonAsync("/v1/auth/refresh", new { refreshToken });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("USER_DISABLED", problem.GetProperty("appbridgeCode").GetString());
    }

    [Fact]
    public async Task Refresh_for_a_suspended_tenant_is_rejected_with_403_TENANT_SUSPENDED()
    {
        var refreshToken = await LoginAndGetRefreshTokenAsync();
        await using (var setup = NewDbContext())
        {
            var tenant = await setup.Tenants.SingleAsync(t => t.Id == _tenantId);
            tenant.Status = TenantStatus.Suspended;
            await setup.SaveChangesAsync();
        }

        var response = await _client.PostAsJsonAsync("/v1/auth/refresh", new { refreshToken });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("TENANT_SUSPENDED", problem.GetProperty("appbridgeCode").GetString());
    }

    [Fact]
    public async Task Logout_revokes_the_token_and_writes_a_logout_access_event()
    {
        var refreshToken = await LoginAndGetRefreshTokenAsync();

        var response = await _client.PostAsJsonAsync("/v1/auth/logout", new { refreshToken });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await using var verify = NewDbContext();
        var tokenRow = await verify.RefreshTokens.SingleAsync();
        Assert.NotNull(tokenRow.RevokedAt);

        var logoutEvent = await verify.AccessEvents.SingleAsync(e => e.EventType == "logout");
        Assert.Equal(_userAccountId, logoutEvent.UserAccountId);
    }

    [Fact]
    public async Task Logout_makes_a_subsequent_refresh_fail()
    {
        var refreshToken = await LoginAndGetRefreshTokenAsync();
        await _client.PostAsJsonAsync("/v1/auth/logout", new { refreshToken });

        var response = await _client.PostAsJsonAsync("/v1/auth/refresh", new { refreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("REFRESH_EXPIRED", problem.GetProperty("appbridgeCode").GetString());
    }

    [Fact]
    public async Task Logout_with_an_unknown_or_already_revoked_token_is_still_idempotently_204()
    {
        var unknownTokenResponse = await _client.PostAsJsonAsync("/v1/auth/logout", new { refreshToken = "never-issued" });
        Assert.Equal(HttpStatusCode.NoContent, unknownTokenResponse.StatusCode);

        var refreshToken = await LoginAndGetRefreshTokenAsync();
        await _client.PostAsJsonAsync("/v1/auth/logout", new { refreshToken }); // first logout, real revoke

        var secondLogout = await _client.PostAsJsonAsync("/v1/auth/logout", new { refreshToken });
        Assert.Equal(HttpStatusCode.NoContent, secondLogout.StatusCode);

        // Only one access_event for the logout — the second call didn't write a redundant one.
        await using var verify = NewDbContext();
        Assert.Single(await verify.AccessEvents.Where(e => e.EventType == "logout").ToListAsync());
    }

    [Fact]
    public async Task Malformed_request_body_missing_the_refresh_token_is_a_400_not_a_500()
    {
        var response = await _client.PostAsync(
            "/v1/auth/refresh", new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
