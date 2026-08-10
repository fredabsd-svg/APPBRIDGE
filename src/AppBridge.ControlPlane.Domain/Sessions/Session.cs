using AppBridge.ControlPlane.Domain.Common;

namespace AppBridge.ControlPlane.Domain.Sessions;

/// <summary>
/// MODELO-DE-DADOS.md §6.2. The source of truth for the licence count (ADR-0006) — and therefore
/// where R-009 lives or dies, via <see cref="EndReason"/>.
/// </summary>
public sealed class Session : TenantScopedEntity
{
    /// <summary>Composite FK <c>fk_session_user_account</c> (ADR-0011 §4, T-204).</summary>
    public Guid UserAccountId { get; set; }

    /// <summary>Composite FK <c>fk_session_session_host</c> (ADR-0011 §4, T-204).</summary>
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
