using AppBridge.ControlPlane.Domain.Common;

namespace AppBridge.ControlPlane.Domain.Tenancy;

/// <summary>
/// MODELO-DE-DADOS.md §3.2. One row per tenant × category. The per-category minimum is enforced
/// by a database CHECK constraint (Infrastructure/Configurations), not just here — the protection
/// exists specifically against a client instructing a lower value, so it has to survive a caller
/// that skips the application layer entirely.
/// </summary>
public sealed class RetentionPolicy : TenantScopedEntity
{
    public RetentionCategory Category { get; set; }

    public int RetentionMonths { get; set; }
}
