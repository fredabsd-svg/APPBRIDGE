using AppBridge.ControlPlane.Application.Abstractions.Applications;
using AppBridge.ControlPlane.Application.Abstractions.Authorization;
using AppBridge.ControlPlane.Application.Abstractions.Sessions;
using AppBridge.ControlPlane.Application.Dtos.Sessions;
using AppBridge.ControlPlane.Core.Entities;
using AppBridge.ControlPlane.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AppBridge.ControlPlane.Infrastructure.Services.Sessions;

public class SessionService : ISessionService
{
    private readonly AppBridgeDbContext _dbContext;
    private readonly IAuthorizationService _authorizationService;
    private readonly IRdpFileSigner _rdpFileSigner;
    private readonly ILogger<SessionService> _logger;

    public SessionService(
        AppBridgeDbContext dbContext,
        IAuthorizationService authorizationService,
        IRdpFileSigner rdpFileSigner,
        ILogger<SessionService> logger)
    {
        _dbContext = dbContext;
        _authorizationService = authorizationService;
        _rdpFileSigner = rdpFileSigner;
        _logger = logger;
    }

    public async Task<SessionDto?> GetSessionAsync(Guid tenantId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await _dbContext.Sessions
            .Where(s => s.TenantId == tenantId && s.Id == sessionId)
            .FirstOrDefaultAsync(cancellationToken);

        if (session == null)
        {
            _logger.LogWarning("Session {SessionId} not found in tenant {TenantId}", sessionId, tenantId);
            return null;
        }

        return MapToDto(session);
    }

    public async Task<IEnumerable<SessionDto>> ListUserSessionsAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var sessions = await _dbContext.Sessions
            .Where(s => s.TenantId == tenantId && s.UserId == userId && s.State != SessionState.Terminated)
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Retrieved {Count} active sessions for user {UserId} in tenant {TenantId}", sessions.Count, userId, tenantId);

        return sessions.Select(MapToDto);
    }

    public async Task<LaunchSessionResponseDto> LaunchSessionAsync(Guid tenantId, Guid userId, Guid applicationId, CancellationToken cancellationToken = default)
    {
        var application = await _dbContext.Applications
            .Where(a => a.TenantId == tenantId && a.Id == applicationId && a.DeletedAt == null)
            .FirstOrDefaultAsync(cancellationToken);

        if (application == null)
        {
            _logger.LogWarning("Application {ApplicationId} not found in tenant {TenantId}", applicationId, tenantId);
            throw new KeyNotFoundException($"Application {applicationId} not found in tenant {tenantId}");
        }

        var hasAccess = await _authorizationService.UserHasApplicationAccessAsync(userId, tenantId, applicationId, cancellationToken);
        if (!hasAccess)
        {
            _logger.LogWarning("User {UserId} denied access to application {ApplicationId} in tenant {TenantId}", userId, applicationId, tenantId);
            throw new UnauthorizedAccessException($"User does not have access to application {applicationId}");
        }

        if (!application.IsPublished)
        {
            _logger.LogWarning("Application {ApplicationId} is not published in tenant {TenantId}", applicationId, tenantId);
            throw new InvalidOperationException($"Application {applicationId} is not published");
        }

        var rdpContent = GenerateRdpContent(application);
        byte[] signedRdp;
        try
        {
            signedRdp = await _rdpFileSigner.SignRdpFileAsync(rdpContent, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sign RDP file for application {ApplicationId}, user {UserId}, tenant {TenantId}", applicationId, userId, tenantId);
            throw new InvalidOperationException("Failed to sign RDP file", ex);
        }

        var session = new Session
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            ApplicationId = applicationId,
            State = SessionState.Pending,
            RdpFileSignature = Convert.ToBase64String(signedRdp),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _dbContext.Sessions.Add(session);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Session {SessionId} created for user {UserId}, application {ApplicationId}, tenant {TenantId}", session.Id, userId, applicationId, tenantId);

        return new LaunchSessionResponseDto
        {
            SessionId = session.Id,
            RdpFile = Convert.ToBase64String(signedRdp),
            ValiditySeconds = 60,
        };
    }

    public async Task<SessionDto> UpdateSessionAsync(Guid tenantId, Guid sessionId, UpdateSessionDto dto, CancellationToken cancellationToken = default)
    {
        var session = await _dbContext.Sessions
            .Where(s => s.TenantId == tenantId && s.Id == sessionId)
            .FirstOrDefaultAsync(cancellationToken);

        if (session == null)
        {
            throw new KeyNotFoundException($"Session {sessionId} not found in tenant {tenantId}");
        }

        if (dto.State.HasValue)
        {
            session.State = dto.State.Value;
        }

        if (dto.SessionHostId != null)
        {
            session.SessionHostId = dto.SessionHostId;
        }

        if (dto.StartedAt.HasValue)
        {
            session.StartedAt = dto.StartedAt.Value;
        }

        if (dto.EndedAt.HasValue)
        {
            session.EndedAt = dto.EndedAt.Value;
        }

        session.UpdatedAt = DateTimeOffset.UtcNow;

        _dbContext.Sessions.Update(session);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Session {SessionId} updated in tenant {TenantId}", sessionId, tenantId);

        return MapToDto(session);
    }

    public async Task<SessionDto> TerminateSessionAsync(Guid tenantId, Guid sessionId, string? reason = null, CancellationToken cancellationToken = default)
    {
        var session = await _dbContext.Sessions
            .Where(s => s.TenantId == tenantId && s.Id == sessionId)
            .FirstOrDefaultAsync(cancellationToken);

        if (session == null)
        {
            throw new KeyNotFoundException($"Session {sessionId} not found in tenant {tenantId}");
        }

        session.State = SessionState.Terminated;
        session.TerminationReason = reason;
        session.EndedAt = DateTimeOffset.UtcNow;
        session.UpdatedAt = DateTimeOffset.UtcNow;

        _dbContext.Sessions.Update(session);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Session {SessionId} terminated in tenant {TenantId} with reason: {Reason}", sessionId, tenantId, reason ?? "none provided");

        return MapToDto(session);
    }

    private static string GenerateRdpContent(Application application)
    {
        var rdpContent = new System.Text.StringBuilder();
        rdpContent.AppendLine("auto connect:i:1");
        rdpContent.AppendLine("full address:s:localhost");
        rdpContent.AppendLine($"remoteapplicationname:s:{application.RemoteAppName}");
        rdpContent.AppendLine("remoteapplicationmode:i:1");
        rdpContent.AppendLine("compression:i:1");
        rdpContent.AppendLine("audiomode:i:0");
        rdpContent.AppendLine("redirectprinters:i:1");
        rdpContent.AppendLine("redirectcomports:i:0");
        rdpContent.AppendLine("redirectsmartcards:i:1");
        rdpContent.AppendLine("redirectclipboard:i:1");
        rdpContent.AppendLine("redirectposdevices:i:0");

        return rdpContent.ToString();
    }

    private static SessionDto MapToDto(Session session) =>
        new()
        {
            Id = session.Id,
            ApplicationId = session.ApplicationId,
            UserId = session.UserId,
            State = session.State,
            SessionHostId = session.SessionHostId,
            TerminationReason = session.TerminationReason,
            StartedAt = session.StartedAt,
            EndedAt = session.EndedAt,
            DurationSeconds = session.DurationSeconds,
            IsCountedInLicense = session.IsCountedInLicense,
            CreatedAt = session.CreatedAt,
            UpdatedAt = session.UpdatedAt,
        };
}
