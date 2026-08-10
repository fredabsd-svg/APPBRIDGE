using AppBridge.ControlPlane.Application.Abstractions.Authentication;
using AppBridge.ControlPlane.Application.Dtos.Authentication;
using AppBridge.ControlPlane.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AppBridge.ControlPlane.Infrastructure.Services.Authentication;

public class AuthenticationService : IAuthenticationService
{
    private readonly AppBridgeDbContext _dbContext;
    private readonly ITokenService _tokenService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<AuthenticationService> _logger;

    public AuthenticationService(
        AppBridgeDbContext dbContext,
        ITokenService tokenService,
        IPasswordHasher passwordHasher,
        ILogger<AuthenticationService> logger)
    {
        _dbContext = dbContext;
        _tokenService = tokenService;
        _passwordHasher = passwordHasher;
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

        if (!_passwordHasher.VerifyPassword(password, user.PasswordHash))
        {
            _logger.LogWarning("Authentication failed: invalid password for user {UserIdentifier} in tenant {TenantId}", userIdentifier, tenantId);
            throw new UnauthorizedAccessException("Invalid credentials");
        }

        var token = _tokenService.GenerateToken(user.Id.ToString(), user.Identifier, tenantId);

        _logger.LogInformation("User {UserIdentifier} authenticated successfully in tenant {TenantId}", userIdentifier, tenantId);

        user.LastLoginAt = DateTimeOffset.UtcNow;
        _dbContext.Users.Update(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new AuthTokenDto
        {
            AccessToken = token,
            ExpiresIn = 3600,
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
