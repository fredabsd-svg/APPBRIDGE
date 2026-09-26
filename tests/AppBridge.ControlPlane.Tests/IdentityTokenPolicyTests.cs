using System.Globalization;
using System.Security.Claims;
using AppBridge.ControlPlane.Authentication;
using AppBridge.ControlPlane.Data;
using AppBridge.ControlPlane.Domain.Entities;
using AppBridge.ControlPlane.Domain.Enums;
using AppBridge.ControlPlane.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AppBridge.ControlPlane.Tests;

// ADR-0023 · troca por access token recente e de uso único (RC-05).
public sealed partial class TenantIsolationTests
{
    private static readonly IdentityProviderOptions PolicyOptions = new()
    {
        ClientApplicationId = TestLauncherClientId,
        RequiredScope = "access_as_user",
        MaxTokenAgeMinutes = 10
    };

    [Fact]
    public void Identity_token_policy_accepts_a_fresh_api_token_from_the_launcher()
    {
        var now = DateTimeOffset.UtcNow;
        var result = IdentityTokenPolicy.Evaluate(PolicyToken(now), PolicyOptions, now);

        Assert.True(result.Accepted);
        Assert.Equal(64, result.TokenHash!.Length);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(now.AddMinutes(59).ToUnixTimeSeconds()), result.ExpiresAt);

        // Tokens v1 trazem o cliente em `appid` e o identificador em `jti`.
        var v1 = IdentityTokenPolicy.Evaluate(PolicyToken(now, azp: null, appid: TestLauncherClientId, uti: null, jti: "v1-token"), PolicyOptions, now);
        Assert.True(v1.Accepted);
    }

    [Theory]
    [InlineData("scope")]
    [InlineData("client")]
    [InlineData("age")]
    [InlineData("future")]
    [InlineData("token_id")]
    [InlineData("lifetime")]
    public void Identity_token_policy_rejects_tokens_outside_the_contract(string violation)
    {
        var now = DateTimeOffset.UtcNow;
        var token = violation switch
        {
            // O ID token do launcher não tem `scp`: é exatamente o que o ADR-0023 deixa de aceitar.
            "scope" => PolicyToken(now, scp: "openid profile"),
            "client" => PolicyToken(now, azp: "another-app"),
            "age" => PolicyToken(now, issuedAt: now.AddMinutes(-12)),
            "future" => PolicyToken(now, issuedAt: now.AddMinutes(5)),
            "token_id" => PolicyToken(now, uti: null),
            _ => PolicyToken(now, includeLifetime: false)
        };

        var result = IdentityTokenPolicy.Evaluate(token, PolicyOptions, now);

        Assert.False(result.Accepted);
        Assert.Equal(violation == "future" ? "age" : violation, result.Reason);
    }

    [Fact]
    public void Identity_token_policy_requires_the_launcher_client_to_be_configured()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.Throws<IdentityProviderUnavailableException>(() =>
            IdentityTokenPolicy.Evaluate(PolicyToken(now), new IdentityProviderOptions(), now));
    }

    [Fact]
    public async Task Replayed_entra_token_is_refused_and_audited()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantId = Guid.CreateVersion7();
        await AddTenantAsync(tenantId);
        var externalSubject = $"replay-{tenantId:N}";
        await using (var seed = CreateContext(tenantId))
        {
            seed.UserAccounts.Add(new UserAccount
            {
                TenantId = tenantId,
                ExternalSubject = externalSubject,
                Upn = "replay@example.test",
                AdObjectSid = $"S-1-5-21-replay-{tenantId:N}",
                DisplayName = "Replay",
                Email = "replay@example.test",
                Status = UserAccountStatus.Active
            });
            await seed.SaveChangesAsync(cancellationToken);
        }

        async Task<AuthenticationSessionResult> ExchangeAsync()
        {
            var tenantContext = new TenantContext();
            await using var db = CreateContext(tenantContext);
            var service = CreateAuthenticationSessionService(
                db, tenantContext, "entra-replay", tenantId, externalSubject, fixedTokenId: "captured-token");
            return await service.ExchangeAsync(
                new AuthenticationSessionRequest("captured", "PC-REPLAY"), "10.0.0.9", Guid.CreateVersion7(), cancellationToken);
        }

        var first = await ExchangeAsync();
        var replay = await ExchangeAsync();

        Assert.Equal(StatusCodes.Status201Created, first.StatusCode);
        Assert.Equal(StatusCodes.Status401Unauthorized, replay.StatusCode);
        Assert.Equal("INVALID_IDENTITY_TOKEN", replay.ErrorCode);
        Assert.Null(replay.Response);

        await using var verification = CreateContext(tenantId);
        Assert.Single(await verification.IdentityTokenRedemptions.ToListAsync(cancellationToken));
        Assert.Single(await verification.AuthenticationSessions.ToListAsync(cancellationToken));
        var denied = await verification.AccessEvents.SingleAsync(
            row => row.EventType == AccessEventType.Authentication && row.Result == AccessEventResult.Failure,
            cancellationToken);
        Assert.Equal("IDENTITY_TOKEN_REPLAYED", denied.FailureReason);
        Assert.Equal("10.0.0.9", denied.SourceIp);
    }

    private static ClaimsPrincipal PolicyToken(
        DateTimeOffset now,
        string? scp = "access_as_user",
        string? azp = TestLauncherClientId,
        string? appid = null,
        DateTimeOffset? issuedAt = null,
        string? uti = "token-1",
        string? jti = null,
        bool includeLifetime = true)
    {
        var claims = new List<Claim>();
        void Add(string type, string? value)
        {
            if (value is not null)
            {
                claims.Add(new Claim(type, value));
            }
        }

        Add("scp", scp);
        Add("azp", azp);
        Add("appid", appid);
        Add("uti", uti);
        Add("jti", jti);
        if (includeLifetime)
        {
            Add("iat", (issuedAt ?? now.AddMinutes(-1)).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
            Add("exp", now.AddMinutes(59).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }
}
