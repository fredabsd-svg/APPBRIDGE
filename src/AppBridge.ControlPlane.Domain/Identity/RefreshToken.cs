using AppBridge.ControlPlane.Domain.Common;

namespace AppBridge.ControlPlane.Domain.Identity;

/// <summary>
/// MODELO-DE-DADOS.md §4.3, ADR-0017. Not a trail table: <see cref="RevokedAt"/> must be writable
/// after creation (logout or rotation in T-303), which <c>AppendOnlyEntity</c> deliberately forbids.
/// </summary>
public sealed class RefreshToken : TenantScopedEntity
{
    /// <summary>Composite FK <c>fk_refresh_token_user_account</c> (ADR-0011 §4).</summary>
    public Guid UserAccountId { get; set; }

    /// <summary>SHA-256 of the opaque value handed to the client — never the value itself (ADR-0017).</summary>
    public required string TokenHash { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Null means still valid. Set on logout or on rotation by a later refresh (T-303).</summary>
    public DateTimeOffset? RevokedAt { get; set; }
}
