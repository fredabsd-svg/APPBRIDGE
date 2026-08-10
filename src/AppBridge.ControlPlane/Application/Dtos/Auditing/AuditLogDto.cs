namespace AppBridge.ControlPlane.Application.Dtos.Auditing;

public class AuditLogDto
{
    public Guid TenantId { get; set; }
    public string Category { get; set; } = null!;
    public Guid? ActorUserId { get; set; }
    public string ActorIdentifier { get; set; } = null!;
    public string Action { get; set; } = null!;
    public string ResourceType { get; set; } = null!;
    public string ResourceId { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string Result { get; set; } = null!;
    public string? FailureReason { get; set; }
    public string? SourceIp { get; set; }
    public string? UserAgent { get; set; }
    public string? Details { get; set; }
}
