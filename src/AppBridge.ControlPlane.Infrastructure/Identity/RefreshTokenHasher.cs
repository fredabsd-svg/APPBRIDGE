using System.Security.Cryptography;
using System.Text;

namespace AppBridge.ControlPlane.Infrastructure.Identity;

/// <summary>
/// ADR-0017 §2: the refresh token stored in the database is always the SHA-256 hash of the opaque
/// value handed to the client, never the value itself — a database leak shouldn't hand out a usable
/// token, same reasoning as never storing a password in plaintext. Shared here because T-303's
/// refresh/logout endpoints hash the token presented by the client the same way, to look it up.
/// </summary>
public static class RefreshTokenHasher
{
    /// <summary>Generates a new 256-bit opaque token and returns both the raw value (for the client) and its hash (for storage).</summary>
    public static (string RawValue, string Hash) GenerateAndHash()
    {
        var rawValue = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        return (rawValue, Hash(rawValue));
    }

    public static string Hash(string rawValue) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawValue)));
}
