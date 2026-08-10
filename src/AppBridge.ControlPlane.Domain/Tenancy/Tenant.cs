using AppBridge.ControlPlane.Domain.Common;

namespace AppBridge.ControlPlane.Domain.Tenancy;

/// <summary>
/// MODELO-DE-DADOS.md §3.1. The one entity in the whole schema that is NOT tenant-scoped — it is
/// the tenant (ADR-0011 §1).
/// </summary>
public sealed class Tenant : AuditedEntity
{
    public required string Name { get; set; }

    /// <summary>Human-readable identifier used in administrative routes.</summary>
    public required string Slug { get; set; }

    public TenantStatus Status { get; set; } = TenantStatus.Active;

    /// <summary>AD DS domain that serves this tenant (ADR-0001).</summary>
    public string? AdDomain { get; set; }

    /// <summary>Dedicated OU distinguished name for this tenant (ADR-0004).</summary>
    public string? AdOuDn { get; set; }
}
