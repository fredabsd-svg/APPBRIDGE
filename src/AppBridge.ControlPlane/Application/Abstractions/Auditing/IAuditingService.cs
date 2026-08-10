using AppBridge.ControlPlane.Application.Dtos.Auditing;

namespace AppBridge.ControlPlane.Application.Abstractions.Auditing;

public interface IAuditingService
{
    Task LogAccessAsync(AuditLogDto auditLog, CancellationToken cancellationToken = default);
    Task LogAuthorizationAsync(AuditLogDto auditLog, CancellationToken cancellationToken = default);
    Task LogAdministrativeAsync(AuditLogDto auditLog, CancellationToken cancellationToken = default);
    Task LogSystemConfigurationAsync(AuditLogDto auditLog, CancellationToken cancellationToken = default);
}
