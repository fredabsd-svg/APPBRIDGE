namespace AppBridge.ControlPlane.Core.Entities;

/// <summary>
/// Audit trail for all security-relevant events.
/// Blocker audit: event logged in same transaction as action (ADR-0007).
/// Immutable: never updated or deleted (ADR-0011), only inserted.
/// Multi-tenant: explicitly bound to Tenant (ADR-0004).
/// Retenção: 12/24/60 meses by category (ADR-0007).
/// </summary>
public class AuditLog
{
    public Guid Id { get; set; }

    /// <summary>
    /// Tenant ID (foreign key + isolation boundary).
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Category of audit event (RNF-018, RNF-022).
    /// Values: Access, Authorization, Administrative, SystemConfiguration.
    /// Retention policy tied to category.
    /// </summary>
    public AuditCategory Category { get; set; }

    /// <summary>
    /// User who triggered the event (null = system action).
    /// </summary>
    public Guid? ActorUserId { get; set; }

    /// <summary>
    /// Actor identifier for audit (email/UPN if User exists, "system" if null).
    /// Denormalized for resilience (user may be deleted).
    /// </summary>
    public string ActorIdentifier { get; set; } = null!;

    /// <summary>
    /// Type of action (launch, revoke-access, login, create-app, etc.).
    /// </summary>
    public string Action { get; set; } = null!;

    /// <summary>
    /// Resource type affected (User, Application, Session, etc.).
    /// </summary>
    public string ResourceType { get; set; } = null!;

    /// <summary>
    /// Resource ID (user ID, app ID, session ID, etc.).
    /// </summary>
    public string ResourceId { get; set; } = null!;

    /// <summary>
    /// Human-readable description of what happened.
    /// </summary>
    public string Description { get; set; } = null!;

    /// <summary>
    /// Result of the action (Success, Failure, Denied).
    /// Denormalized for querying (RNF-020).
    /// </summary>
    public AuditResult Result { get; set; }

    /// <summary>
    /// Failure reason if Result = Failure or Denied (RNF-020).
    /// </summary>
    public string? FailureReason { get; set; }

    /// <summary>
    /// IP address of the request origin (for Access category).
    /// Null if not applicable (e.g., Administrative action by system).
    /// </summary>
    public string? SourceIp { get; set; }

    /// <summary>
    /// User agent string (for Access category).
    /// Helps identify client OS/version.
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Free-form details (JSON) for extensibility (RNF-020).
    /// Example: { "appId": "...", "reason": "...", "oldValue": "...", "newValue": "..." }
    /// Logged before/after audit trail starts (ADR-0007).
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// Timestamp when event occurred (UTC).
    /// Critical for audit ordering (PS-03, encadeamento criptográfico).
    /// </summary>
    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>
    /// Timestamp when event was logged (UTC).
    /// May differ from OccurredAt due to batching (should be rare).
    /// </summary>
    public DateTimeOffset LoggedAt { get; set; }

    /// <summary>
    /// Cryptographic hash for chain verification (PS-03, B-007).
    /// Hash of (previous log's hash || this event's data).
    /// Prevents tampering without detection.
    /// Null until ADR-0015 is decided and implemented.
    /// </summary>
    public string? ChainHash { get; set; }

    /// <summary>
    /// Reference to previous log entry (for chaining, PS-03).
    /// Used with ChainHash to verify integrity.
    /// </summary>
    public Guid? PreviousLogId { get; set; }

    // Navigation properties
    public Tenant Tenant { get; set; } = null!;
    public User? Actor { get; set; }
}

/// <summary>
/// Audit event categories (ADR-0007, RNF-018, RNF-022).
/// Retention policy: Access (12m), Authorization (24m), Administrative (60m), SystemConfiguration (60m).
/// </summary>
public enum AuditCategory
{
    /// <summary>User login, logout, session start/end (RNF-018, 12 months).</summary>
    Access = 0,

    /// <summary>Permission grant, revoke, access denied (RNF-018, 24 months).</summary>
    Authorization = 1,

    /// <summary>Admin actions: create user/app, assign permissions, configure system (60 months).</summary>
    Administrative = 2,

    /// <summary>System configuration changes, policy updates, security settings (60 months).</summary>
    SystemConfiguration = 3,
}

/// <summary>
/// Audit event result (RNF-020).
/// </summary>
public enum AuditResult
{
    /// <summary>Action completed successfully.</summary>
    Success = 0,

    /// <summary>Action failed due to technical error.</summary>
    Failure = 1,

    /// <summary>Action denied due to authorization (no permission).</summary>
    Denied = 2,
}
