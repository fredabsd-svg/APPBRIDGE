namespace AppBridge.ControlPlane.Domain.Common;

/// <summary>Base for trail tables that belong to a tenant — all three of them in MVP-0.</summary>
public abstract class TenantScopedAppendOnlyEntity : AppendOnlyEntity, ITenantScoped
{
    public Guid TenantId { get; init; }
}
