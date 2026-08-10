using AppBridge.ControlPlane.Application.Abstractions.Auditing;
using AppBridge.ControlPlane.Application.Dtos.Auditing;
using AppBridge.ControlPlane.Core.Entities;
using AppBridge.ControlPlane.Infrastructure.Persistence;

namespace AppBridge.ControlPlane.Infrastructure.Services.Auditing;

public class AuditingService : IAuditingService
{
    private readonly AppBridgeDbContext _dbContext;
    private readonly ILogger<AuditingService> _logger;

    public AuditingService(AppBridgeDbContext dbContext, ILogger<AuditingService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task LogAccessAsync(AuditLogDto auditLog, CancellationToken cancellationToken = default)
    {
        await LogAuditAsync(AuditCategory.Access, auditLog, cancellationToken);
    }

    public async Task LogAuthorizationAsync(AuditLogDto auditLog, CancellationToken cancellationToken = default)
    {
        await LogAuditAsync(AuditCategory.Authorization, auditLog, cancellationToken);
    }

    public async Task LogAdministrativeAsync(AuditLogDto auditLog, CancellationToken cancellationToken = default)
    {
        await LogAuditAsync(AuditCategory.Administrative, auditLog, cancellationToken);
    }

    public async Task LogSystemConfigurationAsync(AuditLogDto auditLog, CancellationToken cancellationToken = default)
    {
        await LogAuditAsync(AuditCategory.SystemConfiguration, auditLog, cancellationToken);
    }

    private async Task LogAuditAsync(AuditCategory category, AuditLogDto auditLog, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = Enum.TryParse<AuditResult>(auditLog.Result, out var parsedResult) ? parsedResult : AuditResult.Success;

            var auditLogEntity = new AuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = auditLog.TenantId,
                Category = category,
                ActorUserId = auditLog.ActorUserId,
                ActorIdentifier = auditLog.ActorIdentifier,
                Action = auditLog.Action,
                ResourceType = auditLog.ResourceType,
                ResourceId = auditLog.ResourceId,
                Description = auditLog.Description,
                Result = result,
                FailureReason = auditLog.FailureReason,
                SourceIp = auditLog.SourceIp,
                UserAgent = auditLog.UserAgent,
                Details = auditLog.Details,
                OccurredAt = DateTimeOffset.UtcNow,
                LoggedAt = DateTimeOffset.UtcNow,
            };

            _dbContext.AuditLogs.Add(auditLogEntity);

            // BLOCKER PATTERN (ADR-0007): Auditoria é bloqueante
            // A falha ao salvar o log impede que a ação seja completada
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Audit log recorded: Category={Category} Action={Action} Resource={ResourceType}/{ResourceId} Result={Result} Tenant={TenantId}",
                category, auditLog.Action, auditLog.ResourceType, auditLog.ResourceId, result, auditLog.TenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log audit event. Blocking action. Category={Category} Action={Action} Tenant={TenantId}",
                category, auditLog.Action, auditLog.TenantId);

            throw new InvalidOperationException(
                $"Audit logging failed for action '{auditLog.Action}'. The operation cannot proceed.",
                ex);
        }
    }
}
