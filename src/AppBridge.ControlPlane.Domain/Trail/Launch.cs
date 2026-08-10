using AppBridge.ControlPlane.Domain.Common;

namespace AppBridge.ControlPlane.Domain.Trail;

/// <summary>
/// MODELO-DE-DADOS.md §7.1. This IS the trail of RF-037 (launch) and RF-039 (denied authorization)
/// — a denial is a row here with <see cref="Outcome"/> = DeniedPermission, not a separate table.
/// Retention category: access (ADR-0007).
/// </summary>
public sealed class Launch : TenantScopedAppendOnlyEntity
{
    // TODO(T-204): ADR-0011 §4 composite FK (tenant_id, user_account_id) -> user_account(tenant_id, id).
    public Guid UserAccountId { get; set; }

    // TODO(T-204): ADR-0011 §4 composite FK (tenant_id, application_id) -> application(tenant_id, id).
    public Guid ApplicationId { get; set; }

    /// <summary>Set once the session is created or reused.</summary>
    public Guid? SessionId { get; set; }

    public DateTimeOffset RequestedAt { get; init; }

    public LaunchOutcome Outcome { get; set; }

    public string? DenialReason { get; set; }

    public string? SourceIp { get; set; }

    public string? WorkstationName { get; set; }

    /// <summary>60 s TTL (PRE-07) — the launcher writes the .rdp, runs it, deletes it.</summary>
    public DateTimeOffset RdpExpiresAt { get; set; }

    /// <summary>Ties launcher → Control Plane → host together in the structured log (RNF-039).</summary>
    public Guid CorrelationId { get; set; }

    /// <summary>user_initiated vs prelaunch (ADR-0016, Gap 1) — every metering query filters on this.</summary>
    public LaunchPurpose Purpose { get; set; }
}
