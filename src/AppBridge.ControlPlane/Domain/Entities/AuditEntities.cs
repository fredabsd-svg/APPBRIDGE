using System.Text.Json;
using AppBridge.ControlPlane.Domain.Enums;

namespace AppBridge.ControlPlane.Domain.Entities;

public sealed class Launch : TenantAppendOnlyEntity
{
    public Guid UserAccountId { get; set; }
    public Guid ApplicationId { get; set; }
    public Guid? SessionId { get; set; }
    public LaunchPurpose Purpose { get; set; }
    public DateTimeOffset RequestedAt { get; set; }
    public LaunchOutcome Outcome { get; set; }
    public string? DenialReason { get; set; }
    public string SourceIp { get; set; } = string.Empty;
    public string WorkstationName { get; set; } = string.Empty;
    public DateTimeOffset RdpExpiresAt { get; set; }
    public Guid CorrelationId { get; set; }
}

public sealed class AccessEvent : TenantAppendOnlyEntity
{
    public Guid? UserAccountId { get; set; }
    public AccessEventType EventType { get; set; }
    public AccessEventResult Result { get; set; }
    public string? FailureReason { get; set; }
    public string SourceIp { get; set; } = string.Empty;
    public string WorkstationName { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
    public Guid CorrelationId { get; set; }
    public JsonDocument? Payload { get; set; }
}

public sealed class PurgeRun : TenantAppendOnlyEntity
{
    public RetentionCategory Category { get; set; }
    public DateOnly CutoffDate { get; set; }
    public long RowsDeleted { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string Outcome { get; set; } = string.Empty;
}

/// <summary>Resposta idempotente curta; o corpo é apagado ao expirar e a chave permanece como tombstone.</summary>
public sealed class LaunchIdempotencyRecord : TenantMutableEntity
{
    public Guid IdempotencyKey { get; set; }
    public string RequestHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public string? ResponseJson { get; set; }
}
