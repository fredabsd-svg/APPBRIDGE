using AppBridge.ControlPlane.Application.Dtos.Authentication;

namespace AppBridge.ControlPlane.Application.Abstractions.Authentication;

public interface IAuthenticationService
{
    Task<AuthTokenDto> AuthenticateAsync(string userIdentifier, string password, Guid tenantId, CancellationToken cancellationToken = default);
    Task<bool> ValidateUserAsync(string userIdentifier, Guid tenantId, CancellationToken cancellationToken = default);
}
