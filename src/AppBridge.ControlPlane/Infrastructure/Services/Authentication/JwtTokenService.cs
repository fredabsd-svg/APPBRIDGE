using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AppBridge.ControlPlane.Application.Abstractions.Authentication;
using AppBridge.ControlPlane.Core.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AppBridge.ControlPlane.Infrastructure.Services.Authentication;

public class JwtTokenService : ITokenService
{
    private readonly JwtOptions _jwtOptions;
    private readonly ILogger<JwtTokenService> _logger;

    public JwtTokenService(IOptions<JwtOptions> jwtOptions, ILogger<JwtTokenService> logger)
    {
        _jwtOptions = jwtOptions.Value;
        _logger = logger;
    }

    public string GenerateToken(string userId, string userIdentifier, Guid tenantId)
    {
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SigningKey));
        var signingCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.UniqueName, userIdentifier),
            new("tenant_id", tenantId.ToString()),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtOptions.ExpiryMinutes),
            signingCredentials: signingCredentials);

        var encodedToken = new JwtSecurityTokenHandler().WriteToken(token);
        _logger.LogInformation("JWT token generated for user {UserIdentifier} in tenant {TenantId}", userIdentifier, tenantId);

        return encodedToken;
    }

    public bool ValidateToken(string token)
    {
        try
        {
            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SigningKey));
            var tokenHandler = new JwtSecurityTokenHandler();

            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = signingKey,
                ValidateIssuer = _jwtOptions.Schemes.ValidateIssuer,
                ValidIssuer = _jwtOptions.Issuer,
                ValidateAudience = _jwtOptions.Schemes.ValidateAudience,
                ValidAudience = _jwtOptions.Audience,
                ValidateLifetime = _jwtOptions.Schemes.ValidateLifetime,
                ClockSkew = _jwtOptions.Schemes.ClockSkew,
            }, out SecurityToken validatedToken);

            return validatedToken is JwtSecurityToken;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Token validation failed");
            return false;
        }
    }
}
