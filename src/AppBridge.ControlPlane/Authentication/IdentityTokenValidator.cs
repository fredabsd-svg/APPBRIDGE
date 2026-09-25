using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;

namespace AppBridge.ControlPlane.Authentication;

public interface IIdentityTokenValidator
{
    Task<ClaimsPrincipal> ValidateAsync(string token, CancellationToken cancellationToken);
}

public sealed class IdentityTokenValidator(IOptions<IdentityProviderOptions> options) : IIdentityTokenValidator
{
    private ConfigurationManager<OpenIdConnectConfiguration>? _configurationManager;

    public async Task<ClaimsPrincipal> ValidateAsync(string token, CancellationToken cancellationToken)
    {
        var identity = options.Value;
        if (string.IsNullOrWhiteSpace(identity.Authority) || string.IsNullOrWhiteSpace(identity.Audience))
        {
            throw new IdentityProviderUnavailableException();
        }

        try
        {
            var manager = _configurationManager ??= CreateConfigurationManager(identity.Authority);
            var configuration = await manager.GetConfigurationAsync(cancellationToken);
            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            return handler.ValidateToken(token, new TokenValidationParameters
            {
                RequireSignedTokens = true,
                RequireExpirationTime = true,
                ValidateIssuer = true,
                ValidIssuer = configuration.Issuer,
                ValidateAudience = true,
                ValidAudience = identity.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = configuration.SigningKeys,
                ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1),
                NameClaimType = "name",
                RoleClaimType = "roles"
            }, out _);
        }
        catch (IdentityProviderUnavailableException)
        {
            throw;
        }
        catch (SecurityTokenException exception)
        {
            throw new IdentityTokenRejectedException(exception);
        }
        catch (HttpRequestException exception)
        {
            throw new IdentityProviderUnavailableException(exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new IdentityProviderUnavailableException(exception);
        }
        catch (IOException exception)
        {
            throw new IdentityProviderUnavailableException(exception);
        }
    }

    private static ConfigurationManager<OpenIdConnectConfiguration> CreateConfigurationManager(string authority)
    {
        var normalizedAuthority = authority.TrimEnd('/');
        var metadataAddress = $"{normalizedAuthority}/.well-known/openid-configuration";
        return new ConfigurationManager<OpenIdConnectConfiguration>(
            metadataAddress,
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever { RequireHttps = true });
    }
}

public sealed class IdentityTokenRejectedException(Exception innerException)
    : Exception("O token de identidade foi recusado.", innerException);

public sealed class IdentityProviderUnavailableException(Exception? innerException = null)
    : Exception("O provedor de identidade não está disponível ou não foi configurado.", innerException);
