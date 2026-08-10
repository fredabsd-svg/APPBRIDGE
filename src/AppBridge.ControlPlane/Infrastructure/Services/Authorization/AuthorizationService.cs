using AppBridge.ControlPlane.Application.Abstractions.Authorization;
using AppBridge.ControlPlane.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AppBridge.ControlPlane.Infrastructure.Services.Authorization;

public class AuthorizationService : IAuthorizationService
{
    private readonly AppBridgeDbContext _dbContext;
    private readonly ILogger<AuthorizationService> _logger;

    public AuthorizationService(AppBridgeDbContext dbContext, ILogger<AuthorizationService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<bool> UserHasApplicationAccessAsync(Guid userId, Guid tenantId, Guid applicationId, CancellationToken cancellationToken = default)
    {
        try
        {
            var hasAccess = await _dbContext.ApplicationUserPermissions
                .AnyAsync(
                    p => p.UserId == userId
                        && p.TenantId == tenantId
                        && p.ApplicationId == applicationId
                        && p.RevokedAt == null
                        && (p.ExpiresAt == null || p.ExpiresAt > DateTimeOffset.UtcNow),
                    cancellationToken);

            if (!hasAccess)
            {
                _logger.LogWarning(
                    "User {UserId} denied access to application {ApplicationId} in tenant {TenantId}",
                    userId, applicationId, tenantId);
            }
            else
            {
                _logger.LogInformation(
                    "User {UserId} granted access to application {ApplicationId} in tenant {TenantId}",
                    userId, applicationId, tenantId);
            }

            return hasAccess;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error checking application access for user {UserId} and application {ApplicationId} in tenant {TenantId}",
                userId, applicationId, tenantId);
            return false;
        }
    }

    public async Task<bool> ApplicationIsPublishedAsync(Guid applicationId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        try
        {
            var isPublished = await _dbContext.Applications
                .AnyAsync(
                    a => a.Id == applicationId
                        && a.TenantId == tenantId
                        && a.IsPublished
                        && a.DeletedAt == null,
                    cancellationToken);

            return isPublished;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error checking if application {ApplicationId} is published in tenant {TenantId}",
                applicationId, tenantId);
            return false;
        }
    }

    public async Task<IEnumerable<Guid>> GetUserApplicationsAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        try
        {
            var applicationIds = await _dbContext.ApplicationUserPermissions
                .Where(p => p.UserId == userId
                    && p.TenantId == tenantId
                    && p.RevokedAt == null
                    && (p.ExpiresAt == null || p.ExpiresAt > DateTimeOffset.UtcNow))
                .Select(p => p.ApplicationId)
                .Distinct()
                .ToListAsync(cancellationToken);

            _logger.LogInformation(
                "Retrieved {Count} applications for user {UserId} in tenant {TenantId}",
                applicationIds.Count, userId, tenantId);

            return applicationIds;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error retrieving applications for user {UserId} in tenant {TenantId}",
                userId, tenantId);
            return Enumerable.Empty<Guid>();
        }
    }
}
