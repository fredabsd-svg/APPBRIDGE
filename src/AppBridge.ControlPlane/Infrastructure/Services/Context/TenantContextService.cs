using AppBridge.ControlPlane.Application.Abstractions.Context;

namespace AppBridge.ControlPlane.Infrastructure.Services.Context;

public class TenantContextService : ITenantContextService
{
    private Guid? _tenantId;
    private Guid? _userId;

    public Guid? TenantId => _tenantId;
    public Guid? UserId => _userId;

    public void SetContext(Guid tenantId, Guid userId)
    {
        _tenantId = tenantId;
        _userId = userId;
    }
}
