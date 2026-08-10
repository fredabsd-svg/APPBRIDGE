using AppBridge.ControlPlane.Application.Dtos.Applications;

namespace AppBridge.ControlPlane.Application.Abstractions.Applications;

public interface IApplicationService
{
    Task<ApplicationDto?> GetApplicationAsync(Guid tenantId, Guid applicationId, CancellationToken cancellationToken = default);
    Task<IEnumerable<ApplicationDto>> ListUserApplicationsAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<ApplicationDto>> ListApplicationsAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<ApplicationDto> CreateApplicationAsync(Guid tenantId, CreateApplicationDto dto, CancellationToken cancellationToken = default);
    Task<ApplicationDto> UpdateApplicationAsync(Guid tenantId, Guid applicationId, UpdateApplicationDto dto, CancellationToken cancellationToken = default);
    Task<ApplicationDto> PublishApplicationAsync(Guid tenantId, Guid applicationId, bool isPublished, CancellationToken cancellationToken = default);
    Task DeleteApplicationAsync(Guid tenantId, Guid applicationId, CancellationToken cancellationToken = default);
}
