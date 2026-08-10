using AppBridge.ControlPlane.Application.Dtos.Sessions;

namespace AppBridge.ControlPlane.Application.Abstractions.Sessions;

public interface ISessionService
{
    Task<SessionDto?> GetSessionAsync(Guid tenantId, Guid sessionId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SessionDto>> ListUserSessionsAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<LaunchSessionResponseDto> LaunchSessionAsync(Guid tenantId, Guid userId, Guid applicationId, CancellationToken cancellationToken = default);
    Task<SessionDto> UpdateSessionAsync(Guid tenantId, Guid sessionId, UpdateSessionDto dto, CancellationToken cancellationToken = default);
    Task<SessionDto> TerminateSessionAsync(Guid tenantId, Guid sessionId, string? reason = null, CancellationToken cancellationToken = default);
}
