using System.Security.Cryptography;
using System.Text;

namespace AppBridge.ControlPlane.Authentication;

public sealed class RefreshTokenOptions
{
    public const int DefaultIdleLifetimeDays = 7;
    public const int DefaultMaximumSessionLifetimeDays = 30;

    public int IdleLifetimeDays { get; set; } = DefaultIdleLifetimeDays;
    public int MaximumSessionLifetimeDays { get; set; } = DefaultMaximumSessionLifetimeDays;
}

internal sealed record ParsedRefreshToken(Guid TenantId, Guid SessionId);

internal static class RefreshTokenValue
{
    private const int SecretByteLength = 32;

    public static string Create(Guid tenantId, Guid sessionId)
    {
        var secret = RandomNumberGenerator.GetBytes(SecretByteLength);
        return $"v1.{tenantId:N}.{sessionId:N}.{Base64UrlEncode(secret)}";
    }

    public static string Hash(string refreshToken)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));

    public static bool TryParse(string? value, out ParsedRefreshToken? parsed)
    {
        parsed = null;
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128)
        {
            return false;
        }

        var parts = value.Split('.');
        if (parts.Length != 4
            || parts[0] != "v1"
            || !Guid.TryParseExact(parts[1], "N", out var tenantId)
            || tenantId == Guid.Empty
            || !Guid.TryParseExact(parts[2], "N", out var sessionId)
            || sessionId == Guid.Empty)
        {
            return false;
        }

        try
        {
            var secret = Base64UrlDecode(parts[3]);
            if (secret.Length != SecretByteLength || Base64UrlEncode(secret) != parts[3])
            {
                return false;
            }
        }
        catch (FormatException)
        {
            return false;
        }

        parsed = new ParsedRefreshToken(tenantId, sessionId);
        return true;
    }

    private static string Base64UrlEncode(byte[] value)
        => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight((base64.Length + 3) / 4 * 4, '=');
        return Convert.FromBase64String(base64);
    }
}
