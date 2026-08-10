using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AppBridge.ControlPlane.Domain.Identity;
using AppBridge.ControlPlane.Infrastructure.Identity;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace AppBridge.ControlPlane.Infrastructure.Tests;

/// <summary>
/// No database needed — proves the token ADR-0017 §1 describes actually round-trips through
/// standard JWT validation with matching parameters, independent of the ASP.NET Core pipeline
/// (which has no protected endpoint to exercise it against yet, ADR-0017 §4).
/// </summary>
public sealed class JwtSessionTokenIssuerTests
{
    private const string SigningKey = "unit-test-signing-key-at-least-32-bytes-long!!";

    [Fact]
    public void IssueAccessToken_produces_a_token_that_validates_with_the_same_signing_key()
    {
        var issuer = new JwtSessionTokenIssuer(new JwtSigningOptions { SigningKey = SigningKey });
        var user = NewUser();
        var tenantId = Guid.CreateVersion7();

        var token = issuer.IssueAccessToken(user, tenantId, ["user"]);

        // MapInboundClaims defaults to true: JwtSecurityTokenHandler silently renames some short
        // claim types (e.g. "sub", "unique_name") to long legacy .NET URIs on validation. Disabled
        // so FindFirst matches exactly the claim types the issuer wrote — same setting Program.cs
        // uses for AddJwtBearer, so token creation and consumption agree.
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var principal = handler.ValidateToken(token.Value, ValidationParameters(SigningKey), out var validatedToken);

        Assert.Equal(user.Id.ToString(), principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value);
        Assert.Equal(tenantId.ToString(), principal.FindFirst("tenant_id")?.Value);
        Assert.Equal(user.Upn, principal.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value);
        Assert.Equal("user", principal.FindFirst(ClaimTypes.Role)?.Value);
        Assert.True(validatedToken.ValidTo > DateTime.UtcNow);
        Assert.Equal(token.ExpiresAt.UtcDateTime, validatedToken.ValidTo, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void IssueAccessToken_fails_validation_with_a_different_signing_key()
    {
        var issuer = new JwtSessionTokenIssuer(new JwtSigningOptions { SigningKey = SigningKey });
        var token = issuer.IssueAccessToken(NewUser(), Guid.CreateVersion7(), ["user"]);

        Assert.Throws<SecurityTokenSignatureKeyNotFoundException>(() =>
            new JwtSecurityTokenHandler().ValidateToken(
                token.Value, ValidationParameters("a-completely-different-signing-key-32-bytes!!"), out _));
    }

    private static UserAccount NewUser() => new()
    {
        TenantId = Guid.CreateVersion7(),
        ExternalSubject = "oid-test",
        Upn = "ana@escritorio-a.example",
        AdObjectSid = "S-1-5-21-0-0-0-1001",
    };

    private static TokenValidationParameters ValidationParameters(string signingKey) => new()
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
    };
}
