using AppBridge.ControlPlane.Domain.Common;

namespace AppBridge.ControlPlane.Domain.Trail;

/// <summary>
/// MODELO-DE-DADOS.md §7.2. Access events that are not a launch: authentication (RF-036),
/// session start/end (RF-038), logout. Retention category: access (ADR-0007).
/// </summary>
public sealed class AccessEvent : TenantScopedAppendOnlyEntity
{
    /// <summary>
    /// Null on a failed login attempt against an unknown user — there is no account to point to
    /// yet, and the attempted identifier goes in <see cref="Payload"/>. Without this row, the
    /// product would be blind to credential-stuffing attempts (RNF-010). Composite FK
    /// <c>fk_access_event_user_account</c> (ADR-0011 §4, T-204) when set.
    /// </summary>
    public Guid? UserAccountId { get; set; }

    /// <summary>
    /// Free text, not a closed enum: MODELO-DE-DADOS.md §7.2 leaves the set of event types
    /// unspecified pending the tasks that actually populate them (T-301 login, T-303 logout,
    /// T-601/T-602 session lifecycle). Modelling it as a fixed enum now would mean inventing a
    /// taxonomy nobody decided (RP-05).
    /// </summary>
    public required string EventType { get; set; }

    public AccessEventResult Result { get; set; }

    public string? FailureReason { get; set; }

    public string? SourceIp { get; set; }

    public string? WorkstationName { get; set; }

    public DateTimeOffset OccurredAt { get; init; }

    public Guid CorrelationId { get; set; }

    /// <summary>Whatever is specific to the event type — mapped to <c>jsonb</c>.</summary>
    public string? Payload { get; set; }
}
