using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using AppBridge.ControlPlane.Domain.Entities;

namespace AppBridge.ControlPlane.Authentication;

public sealed class ControlPlaneTokenIssuer(IOptions<ControlPlaneTokenOptions> options)
{
    public (string Token, DateTimeOffset ExpiresAt) Issue(UserAccount user, Tenant tenant, Guid sessionId)
    {
        var tokenOptions = options.Value;
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddMinutes(tokenOptions.LifetimeMinutes);
        var key = new SymmetricSecurityKey(Convert.FromBase64String(tokenOptions.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString("D")),
            new Claim("tenant_id", tenant.Id.ToString("D")),
            new Claim("sid", sessionId.ToString("D")),
            new Claim("name", user.DisplayName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString("D")),
            new Claim(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new Claim(ClaimTypes.Role, "user")
        };
        var token = new JwtSecurityToken(
            tokenOptions.Issuer,
            tokenOptions.Audience,
            claims,
            now.UtcDateTime,
            expiresAt.UtcDateTime,
            credentials);
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        return (handler.WriteToken(token), expiresAt);
    }
}
