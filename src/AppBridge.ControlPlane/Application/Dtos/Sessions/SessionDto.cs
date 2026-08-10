using AppBridge.ControlPlane.Core.Entities;

namespace AppBridge.ControlPlane.Application.Dtos.Sessions;

public class SessionDto
{
    public Guid Id { get; set; }
    public Guid ApplicationId { get; set; }
    public Guid UserId { get; set; }
    public SessionState State { get; set; }
    public string? SessionHostId { get; set; }
    public string? TerminationReason { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public int? DurationSeconds { get; set; }
    public bool IsCountedInLicense { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class CreateSessionDto
{
    public Guid ApplicationId { get; set; }
}

public class UpdateSessionDto
{
    public SessionState? State { get; set; }
    public string? SessionHostId { get; set; }
    public string? TerminationReason { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
}

public class LaunchSessionResponseDto
{
    public Guid SessionId { get; set; }
    public string RdpFile { get; set; } = null!;
    public int ValiditySeconds { get; set; }
}
