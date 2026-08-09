namespace AppBridge.ControlPlane.Core.Entities;

/// <summary>
/// RDP session tracking for license counting and audit (RF-008, RF-038, ADR-0006, ADR-0007).
/// Multi-tenant: explicitly bound to Tenant (ADR-0004).
/// Blocker audit: session start/end is logged before/after this record changes (ADR-0007).
/// </summary>
public class Session
{
    public Guid Id { get; set; }

    /// <summary>
    /// Tenant ID (foreign key + isolation boundary).
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// User launching the application.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Application being launched.
    /// </summary>
    public Guid ApplicationId { get; set; }

    /// <summary>
    /// Session ID assigned by RD Session Host (Connection Broker).
    /// Used to track and terminate sessions (RF-008, RF-038).
    /// </summary>
    public string? SessionHostId { get; set; }

    /// <summary>
    /// State of the session (RFC-038, PRE-23).
    /// Values: Pending, Active, Disconnected, Terminated, Failed.
    /// </summary>
    public SessionState State { get; set; } = SessionState.Pending;

    /// <summary>
    /// Reason for termination if State = Terminated or Failed.
    /// </summary>
    public string? TerminationReason { get; set; }

    /// <summary>
    /// RDP file signature (temporary, 60s validity, PRE-07).
    /// For audit trail only; actual .rdp is signed and served via endpoint.
    /// </summary>
    public string? RdpFileSignature { get; set; }

    /// <summary>
    /// Timestamp when session became active (user connected).
    /// </summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>
    /// Timestamp when session ended (user disconnected or forcibly terminated).
    /// </summary>
    public DateTimeOffset? EndedAt { get; set; }

    /// <summary>
    /// Duration of active session in seconds (calculated from StartedAt to EndedAt).
    /// </summary>
    public int? DurationSeconds
    {
        get
        {
            if (StartedAt.HasValue && EndedAt.HasValue)
            {
                return (int)(EndedAt.Value - StartedAt.Value).TotalSeconds;
            }
            return null;
        }
    }

    /// <summary>
    /// Whether this session has been counted against license limit (RF-062).
    /// Prevents double-counting on reconciliation (SessionReconciler, R-017).
    /// </summary>
    public bool IsCountedInLicense { get; set; } = false;

    /// <summary>
    /// Creation timestamp (UTC) — when launch was requested.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Last update timestamp (UTC) — when state last changed.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation properties
    public Tenant Tenant { get; set; } = null!;
    public User User { get; set; } = null!;
    public Application Application { get; set; } = null!;
}

/// <summary>
/// Session state machine (RF-038, PRE-23).
/// </summary>
public enum SessionState
{
    /// <summary>RDP file generated, waiting for user to open.</summary>
    Pending = 0,

    /// <summary>User has connected to RD Session Host.</summary>
    Active = 1,

    /// <summary>User disconnected (session suspended on host).</summary>
    Disconnected = 2,

    /// <summary>Session explicitly terminated (user logged off or forced).</summary>
    Terminated = 3,

    /// <summary>Launch failed (RDP signing failed, no permission, etc.).</summary>
    Failed = 4,
}
