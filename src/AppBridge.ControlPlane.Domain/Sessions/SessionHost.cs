using AppBridge.ControlPlane.Domain.Common;

namespace AppBridge.ControlPlane.Domain.Sessions;

/// <summary>MODELO-DE-DADOS.md §6.1.</summary>
public sealed class SessionHost : TenantScopedEntity
{
    // TODO(T-204): ADR-0011 §4 composite FK (tenant_id, host_pool_id) -> host_pool(tenant_id, id).
    public Guid HostPoolId { get; set; }

    public required string Fqdn { get; set; }

    public SessionHostStatus Status { get; set; } = SessionHostStatus.Offline;

    /// <summary>Unused until the Agent exists (V2, RF-051) — the column is here so the table isn't ALTERed later.</summary>
    public DateTimeOffset? LastHeartbeatAt { get; set; }

    public int? MaxSessions { get; set; }
}
