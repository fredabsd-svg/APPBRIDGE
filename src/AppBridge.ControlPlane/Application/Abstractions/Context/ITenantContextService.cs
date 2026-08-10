namespace AppBridge.ControlPlane.Application.Abstractions.Context;

public interface ITenantContextService
{
    Guid? TenantId { get; }
    Guid? UserId { get; }
    void SetContext(Guid tenantId, Guid userId);
}
