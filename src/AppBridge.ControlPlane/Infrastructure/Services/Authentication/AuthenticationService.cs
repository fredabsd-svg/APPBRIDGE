using AppBridge.ControlPlane.Application.Abstractions.Authentication;
using AppBridge.ControlPlane.Application.Dtos.Authentication;
using AppBridge.ControlPlane.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AppBridge.ControlPlane.Infrastructure.Services.Authentication;

public class AuthenticationService : IAuthenticationService
{
    private readonly AppBridgeDbContext _dbContext;
    private readonly ITokenService _tokenService;
    private readonly ILogger<AuthenticationService> _logger;

    public AuthenticationService(
        AppBridgeDbContext dbContext,
        ITokenService tokenService,
        ILogger<AuthenticationService> logger)
    {
        _dbContext = dbContext;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<AuthTokenDto> AuthenticateAsync(string userIdentifier, string password, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(
            u => u.TenantId == tenantId && u.Identifier == userIdentifier && u.IsActive && u.DeletedAt == null,
            cancellationToken);

        if (user == null)
        {
            _logger.LogWarning("Authentication failed: user {UserIdentifier} not found or inactive in tenant {TenantId}", userIdentifier, tenantId);
            throw new UnauthorizedAccessException("Invalid credentials");
        }

        // TODO: Implement password verification against hashed password (T-02.6)
        // For MVP-0, this is a placeholder that assumes password validation will be implemented
        // along with proper credential storage (e.g., bcrypt or identity password hasher)

        var token = _tokenService.GenerateToken(user.Id.ToString(), user.Identifier, tenantId);

        _logger.LogInformation("User {UserIdentifier} authenticated successfully in tenant {TenantId}", userIdentifier, tenantId);

        return new AuthTokenDto
        {
            AccessToken = token,
            ExpiresIn = 3600, // 1 hour (align with JwtOptions.ExpiryMinutes)
        };
    }

    public async Task<bool> ValidateUserAsync(string userIdentifier, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(
            u => u.TenantId == tenantId && u.Identifier == userIdentifier && u.IsActive && u.DeletedAt == null,
            cancellationToken);

        return user != null;
    }
}
