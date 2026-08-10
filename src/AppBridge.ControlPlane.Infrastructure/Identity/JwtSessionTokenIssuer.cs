using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AppBridge.ControlPlane.Domain.Identity;
using Microsoft.IdentityModel.Tokens;

namespace AppBridge.ControlPlane.Infrastructure.Identity;

/// <summary>See <see cref="ISessionTokenIssuer"/>. ADR-0017 §1: HMAC-SHA256, claims are sub (UserAccount.Id), tenant_id, upn, role(s), jti, iat, exp.</summary>
public sealed class JwtSessionTokenIssuer(JwtSigningOptions options) : ISessionTokenIssuer
{
    private readonly SigningCredentials _signingCredentials = new(
        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
        SecurityAlgorithms.HmacSha256);

    public SessionToken IssueAccessToken(UserAccount user, Guid tenantId, IReadOnlyList<string> roles)
    {
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.Add(options.AccessTokenTtl);

        List<Claim> claims =
        [
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new("tenant_id", tenantId.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.Upn),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            .. roles.Select(role => new Claim(ClaimTypes.Role, role)),
        ];

        var token = new JwtSecurityToken(
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: _signingCredentials);

        return new SessionToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
