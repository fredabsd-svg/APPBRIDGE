using AppBridge.ControlPlane.Domain.Common;

namespace AppBridge.ControlPlane.Domain.Catalog;

/// <summary>
/// MODELO-DE-DADOS.md §5.2. No deletion — only validity (ADR-0011 §3). Revoking closes
/// <see cref="EffectiveTo"/>; it never removes the row, because RF-007 needs to know the
/// permission once existed.
/// </summary>
public sealed class ApplicationPermission : TenantScopedEntity
{
    // TODO(T-204): ADR-0011 §4 composite FK (tenant_id, application_id) -> application(tenant_id, id).
    public Guid ApplicationId { get; set; }

    // TODO(T-204): ADR-0011 §4 composite FK (tenant_id, group_id) -> group(tenant_id, id).
    public Guid GroupId { get; set; }

    public DateTimeOffset EffectiveFrom { get; set; }

    /// <summary>Null means currently in force. This is the column the launch path filters on.</summary>
    public DateTimeOffset? EffectiveTo { get; set; }

    public Guid GrantedBy { get; set; }

    public Guid? RevokedBy { get; set; }

    public string? RevocationReason { get; set; }
}
