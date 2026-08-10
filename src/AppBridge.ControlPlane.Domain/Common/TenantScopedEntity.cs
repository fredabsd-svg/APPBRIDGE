namespace AppBridge.ControlPlane.Domain.Common;

/// <summary>Base for mutable entities that belong to a tenant — everything except Tenant itself.</summary>
public abstract class TenantScopedEntity : AuditedEntity, ITenantScoped
{
    public Guid TenantId { get; init; }
}
