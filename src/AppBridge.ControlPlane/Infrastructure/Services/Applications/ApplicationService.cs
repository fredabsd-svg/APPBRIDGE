using AppBridge.ControlPlane.Application.Abstractions.Applications;
using AppBridge.ControlPlane.Application.Abstractions.Authorization;
using AppBridge.ControlPlane.Application.Dtos.Applications;
using AppBridge.ControlPlane.Core.Entities;
using AppBridge.ControlPlane.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AppBridge.ControlPlane.Infrastructure.Services.Applications;

public class ApplicationService : IApplicationService
{
    private readonly AppBridgeDbContext _dbContext;
    private readonly IAuthorizationService _authorizationService;
    private readonly ILogger<ApplicationService> _logger;

    public ApplicationService(
        AppBridgeDbContext dbContext,
        IAuthorizationService authorizationService,
        ILogger<ApplicationService> logger)
    {
        _dbContext = dbContext;
        _authorizationService = authorizationService;
        _logger = logger;
    }

    public async Task<ApplicationDto?> GetApplicationAsync(Guid tenantId, Guid applicationId, CancellationToken cancellationToken = default)
    {
        var application = await _dbContext.Applications
            .Where(a => a.TenantId == tenantId && a.Id == applicationId && a.DeletedAt == null)
            .FirstOrDefaultAsync(cancellationToken);

        if (application == null)
        {
            _logger.LogWarning("Application {ApplicationId} not found in tenant {TenantId}", applicationId, tenantId);
            return null;
        }

        return MapToDto(application);
    }

    public async Task<IEnumerable<ApplicationDto>> ListUserApplicationsAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var userApplicationIds = await _authorizationService.GetUserApplicationsAsync(userId, tenantId, cancellationToken);

        if (!userApplicationIds.Any())
        {
            _logger.LogInformation("User {UserId} has no accessible applications in tenant {TenantId}", userId, tenantId);
            return Enumerable.Empty<ApplicationDto>();
        }

        var applications = await _dbContext.Applications
            .Where(a => a.TenantId == tenantId && userApplicationIds.Contains(a.Id) && a.DeletedAt == null)
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Retrieved {Count} accessible applications for user {UserId} in tenant {TenantId}", applications.Count, userId, tenantId);

        return applications.Select(MapToDto);
    }

    public async Task<IEnumerable<ApplicationDto>> ListApplicationsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var applications = await _dbContext.Applications
            .Where(a => a.TenantId == tenantId && a.DeletedAt == null)
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Retrieved {Count} applications in tenant {TenantId}", applications.Count, tenantId);

        return applications.Select(MapToDto);
    }

    public async Task<ApplicationDto> CreateApplicationAsync(Guid tenantId, CreateApplicationDto dto, CancellationToken cancellationToken = default)
    {
        var application = new Application
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Identifier = dto.Identifier,
            DisplayName = dto.DisplayName,
            Description = dto.Description,
            RemoteAppName = dto.RemoteAppName,
            IconUrl = dto.IconUrl,
            IsPublished = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _dbContext.Applications.Add(application);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Application {ApplicationId} created in tenant {TenantId}", application.Id, tenantId);

        return MapToDto(application);
    }

    public async Task<ApplicationDto> UpdateApplicationAsync(Guid tenantId, Guid applicationId, UpdateApplicationDto dto, CancellationToken cancellationToken = default)
    {
        var application = await _dbContext.Applications
            .Where(a => a.TenantId == tenantId && a.Id == applicationId && a.DeletedAt == null)
            .FirstOrDefaultAsync(cancellationToken);

        if (application == null)
        {
            throw new KeyNotFoundException($"Application {applicationId} not found in tenant {tenantId}");
        }

        application.DisplayName = dto.DisplayName;
        application.Description = dto.Description;
        application.IconUrl = dto.IconUrl;
        application.UpdatedAt = DateTimeOffset.UtcNow;

        _dbContext.Applications.Update(application);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Application {ApplicationId} updated in tenant {TenantId}", applicationId, tenantId);

        return MapToDto(application);
    }

    public async Task<ApplicationDto> PublishApplicationAsync(Guid tenantId, Guid applicationId, bool isPublished, CancellationToken cancellationToken = default)
    {
        var application = await _dbContext.Applications
            .Where(a => a.TenantId == tenantId && a.Id == applicationId && a.DeletedAt == null)
            .FirstOrDefaultAsync(cancellationToken);

        if (application == null)
        {
            throw new KeyNotFoundException($"Application {applicationId} not found in tenant {tenantId}");
        }

        application.IsPublished = isPublished;
        application.UpdatedAt = DateTimeOffset.UtcNow;

        _dbContext.Applications.Update(application);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Application {ApplicationId} published state changed to {IsPublished} in tenant {TenantId}", applicationId, isPublished, tenantId);

        return MapToDto(application);
    }

    public async Task DeleteApplicationAsync(Guid tenantId, Guid applicationId, CancellationToken cancellationToken = default)
    {
        var application = await _dbContext.Applications
            .Where(a => a.TenantId == tenantId && a.Id == applicationId && a.DeletedAt == null)
            .FirstOrDefaultAsync(cancellationToken);

        if (application == null)
        {
            throw new KeyNotFoundException($"Application {applicationId} not found in tenant {tenantId}");
        }

        application.DeletedAt = DateTimeOffset.UtcNow;
        _dbContext.Applications.Update(application);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Application {ApplicationId} soft-deleted in tenant {TenantId}", applicationId, tenantId);
    }

    private static ApplicationDto MapToDto(Application application) =>
        new()
        {
            Id = application.Id,
            Identifier = application.Identifier,
            DisplayName = application.DisplayName,
            Description = application.Description,
            RemoteAppName = application.RemoteAppName,
            IconUrl = application.IconUrl,
            IsPublished = application.IsPublished,
            CreatedAt = application.CreatedAt,
            UpdatedAt = application.UpdatedAt,
        };
}
