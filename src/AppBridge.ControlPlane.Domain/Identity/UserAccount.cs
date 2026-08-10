using AppBridge.ControlPlane.Domain.Common;

namespace AppBridge.ControlPlane.Domain.Identity;

/// <summary>
/// MODELO-DE-DADOS.md §4.1. Carries both halves of ADR-0001's two-authentication split:
/// <see cref="ExternalSubject"/> identifies who authenticates against the Control Plane;
/// <see cref="AdObjectSid"/> identifies the account that opens the RDS session.
/// </summary>
public sealed class UserAccount : TenantScopedEntity
{
    /// <summary>Stable subject from the identity provider (Entra <c>oid</c>).</summary>
    public required string ExternalSubject { get; set; }

    /// <summary>Must be routable for the hybrid identity model to work (ADR-0001, risks).</summary>
    public required string Upn { get; set; }

    /// <summary>
    /// SID, not sAMAccountName: a display-name or login change in AD does not change the SID, so
    /// the audit trail never ends up pointing at someone who "stopped existing".
    /// </summary>
    public required string AdObjectSid { get; set; }

    public string? DisplayName { get; set; }

    public string? Email { get; set; }

    public UserAccountStatus Status { get; set; } = UserAccountStatus.Active;

    public DateTimeOffset? LastLoginAt { get; set; }
}
