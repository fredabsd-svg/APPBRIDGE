using AppBridge.ControlPlane.Domain.Enums;

namespace AppBridge.ControlPlane.Domain.Entities;

public sealed class HostPool : TenantMutableEntity
{
    public string Name { get; set; } = string.Empty;
    public SessionBackendType BackendType { get; set; }
}

public sealed class SessionHost : TenantMutableEntity
{
    public Guid HostPoolId { get; set; }
    public string Fqdn { get; set; } = string.Empty;
    public SessionHostStatus Status { get; set; }
    public DateTimeOffset? LastHeartbeatAt { get; set; }
    public int MaxSessions { get; set; }
}

public sealed class RemoteSession : TenantMutableEntity
{
    public Guid UserAccountId { get; set; }
    public Guid SessionHostId { get; set; }
    public string BackendSessionId { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public SessionEndReason? EndReason { get; set; }
    public string SourceIp { get; set; } = string.Empty;
    public string WorkstationName { get; set; } = string.Empty;
}
