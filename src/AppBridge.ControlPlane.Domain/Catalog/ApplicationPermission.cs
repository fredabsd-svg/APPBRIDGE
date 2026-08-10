using AppBridge.ControlPlane.Domain.Common;

namespace AppBridge.ControlPlane.Domain.Catalog;

/// <summary>
/// MODELO-DE-DADOS.md §5.2. No deletion — only validity (ADR-0011 §3). Revoking closes
/// <see cref="EffectiveTo"/>; it never removes the row, because RF-007 needs to know the
/// permission once existed.
/// </summary>
public sealed class ApplicationPermission : TenantScopedEntity
{
    /// <summary>Composite FK <c>fk_permission_application</c> (ADR-0011 §4, T-204).</summary>
    public Guid ApplicationId { get; set; }

    /// <summary>Composite FK <c>fk_permission_group</c> (ADR-0011 §4, T-204).</summary>
    public Guid GroupId { get; set; }

    public DateTimeOffset EffectiveFrom { get; set; }

    /// <summary>Null means currently in force. This is the column the launch path filters on.</summary>
    public DateTimeOffset? EffectiveTo { get; set; }

    /// <summary>Composite FK <c>fk_permission_granted_by</c> (ADR-0011 §4, T-204).</summary>
    public Guid GrantedBy { get; set; }

    /// <summary>Composite FK <c>fk_permission_revoked_by</c> (ADR-0011 §4, T-204).</summary>
    public Guid? RevokedBy { get; set; }

    public string? RevocationReason { get; set; }
}
