namespace AppBridge.ControlPlane.Application.Abstractions.Authorization;

public interface IAuthorizationService
{
    Task<bool> UserHasApplicationAccessAsync(Guid userId, Guid tenantId, Guid applicationId, CancellationToken cancellationToken = default);
    Task<bool> ApplicationIsPublishedAsync(Guid applicationId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Guid>> GetUserApplicationsAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default);
}
