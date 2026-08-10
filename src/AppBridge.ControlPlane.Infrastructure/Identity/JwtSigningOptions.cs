namespace AppBridge.ControlPlane.Infrastructure.Identity;

/// <summary>
/// ADR-0017 §1. <see cref="SigningKey"/> comes from the <c>APPBRIDGE_JWT_SIGNING_KEY</c> environment
/// variable — never a file (RP-06) — read once at startup by <c>Program.cs</c>, never logged.
/// </summary>
public sealed class JwtSigningOptions
{
    public required string SigningKey { get; init; }

    /// <summary><c>PREMISSA:</c> 15 minutes (ADR-0017 §1) — not measured, confirm in the dogfood (PRE-29).</summary>
    public TimeSpan AccessTokenTtl { get; init; } = TimeSpan.FromMinutes(15);
}
