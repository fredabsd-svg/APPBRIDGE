using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace AppBridge.ControlPlane.Authentication;

public sealed record IdentityTokenPolicyResult(bool Accepted, string? Reason, string? TokenHash, DateTimeOffset ExpiresAt)
{
    public static IdentityTokenPolicyResult Reject(string reason) => new(false, reason, null, default);
}

/// <summary>
/// Regras do ADR-0023 sobre um access token já validado em assinatura, emissor, audiência e validade:
/// escopo da API, cliente que o pediu, idade e identificador para uso único.
/// </summary>
public static class IdentityTokenPolicy
{
    private static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(1);

    public static IdentityTokenPolicyResult Evaluate(ClaimsPrincipal token, IdentityProviderOptions options, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(options.ClientApplicationId) || string.IsNullOrWhiteSpace(options.RequiredScope))
        {
            // Sem saber qual é o launcher, aceitar seria abrir a troca para qualquer cliente do tenant.
            throw new IdentityProviderUnavailableException();
        }

        var scopes = (token.FindFirst("scp")?.Value ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (!scopes.Contains(options.RequiredScope, StringComparer.Ordinal))
        {
            return IdentityTokenPolicyResult.Reject("scope");
        }

        var client = token.FindFirst("azp")?.Value ?? token.FindFirst("appid")?.Value;
        if (!string.Equals(client, options.ClientApplicationId, StringComparison.OrdinalIgnoreCase))
        {
            return IdentityTokenPolicyResult.Reject("client");
        }

        if (!TryReadEpoch(token, "iat", out var issuedAt) || !TryReadEpoch(token, "exp", out var expiresAt))
        {
            return IdentityTokenPolicyResult.Reject("lifetime");
        }

        var maxAge = TimeSpan.FromMinutes(Math.Clamp(options.MaxTokenAgeMinutes, 1, 60));
        if (issuedAt > now + ClockSkew || now - issuedAt > maxAge + ClockSkew)
        {
            return IdentityTokenPolicyResult.Reject("age");
        }

        var tokenId = token.FindFirst("uti")?.Value ?? token.FindFirst("jti")?.Value;
        if (string.IsNullOrWhiteSpace(tokenId))
        {
            return IdentityTokenPolicyResult.Reject("token_id");
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(tokenId)));
        return new IdentityTokenPolicyResult(true, null, hash, expiresAt);
    }

    private static bool TryReadEpoch(ClaimsPrincipal token, string claim, out DateTimeOffset value)
    {
        value = default;
        if (!long.TryParse(token.FindFirst(claim)?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds))
        {
            return false;
        }

        value = DateTimeOffset.FromUnixTimeSeconds(seconds);
        return true;
    }
}
