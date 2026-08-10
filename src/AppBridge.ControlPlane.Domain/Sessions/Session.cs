using AppBridge.ControlPlane.Domain.Common;

namespace AppBridge.ControlPlane.Domain.Sessions;

/// <summary>
/// MODELO-DE-DADOS.md §6.2. The source of truth for the licence count (ADR-0006) — and therefore
/// where R-009 lives or dies, via <see cref="EndReason"/>.
/// </summary>
public sealed class Session : TenantScopedEntity
{
    // TODO(T-204): ADR-0011 §4 composite FK (tenant_id, user_account_id) -> user_account(tenant_id, id).
    public Guid UserAccountId { get; set; }

    // TODO(T-204): ADR-0011 §4 composite FK (tenant_id, session_host_id) -> session_host(tenant_id, id).
    public Guid SessionHostId { get; set; }

    /// <summary>Identifier on the RDS side — the bridge to <c>ISessionBackend</c>.</summary>
    public required string BackendSessionId { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    /// <summary>Updated by reconciliation; drives <see cref="SessionEndReason.StaleExpired"/>.</summary>
    public DateTimeOffset LastSeenAt { get; set; }

    /// <summary>Null means active. This is the column the launch path filters on.</summary>
    public DateTimeOffset? EndedAt { get; set; }

    public SessionEndReason? EndReason { get; set; }

    public string? SourceIp { get; set; }

    public string? WorkstationName { get; set; }
}
