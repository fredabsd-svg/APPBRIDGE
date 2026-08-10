using AppBridge.ControlPlane.Domain.Common;

namespace AppBridge.ControlPlane.Domain.Sessions;

/// <summary>MODELO-DE-DADOS.md §6.1.</summary>
public sealed class SessionHost : TenantScopedEntity
{
    /// <summary>Composite FK <c>fk_session_host_host_pool</c> (ADR-0011 §4, T-204).</summary>
    public Guid HostPoolId { get; set; }

    public required string Fqdn { get; set; }

    public SessionHostStatus Status { get; set; } = SessionHostStatus.Offline;

    /// <summary>Unused until the Agent exists (V2, RF-051) — the column is here so the table isn't ALTERed later.</summary>
    public DateTimeOffset? LastHeartbeatAt { get; set; }

    public int? MaxSessions { get; set; }
}
