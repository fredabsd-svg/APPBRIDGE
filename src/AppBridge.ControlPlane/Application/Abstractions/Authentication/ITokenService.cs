namespace AppBridge.ControlPlane.Application.Abstractions.Authentication;

public interface ITokenService
{
    string GenerateToken(string userId, string userIdentifier, Guid tenantId);
    bool ValidateToken(string token);
}
