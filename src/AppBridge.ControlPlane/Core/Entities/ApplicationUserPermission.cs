namespace AppBridge.ControlPlane.Core.Entities;

/// <summary>
/// Permission grant: which users can access which applications.
/// Multi-tenant: composite key ensures tenant boundary (ADR-0004, ADR-0011).
/// Blocker revocation: AuditLog entry logged in same transaction (ADR-0007).
/// </summary>
public class ApplicationUserPermission
{
    public Guid Id { get; set; }

    /// <summary>
    /// Tenant ID (foreign key + isolation boundary).
    /// Part of composite key: (tenant_id, application_id, user_id).
    /// Prevents cross-tenant ForeignKey at database level (ADR-0011).
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Application being granted access to.
    /// </summary>
    public Guid ApplicationId { get; set; }

    /// <summary>
    /// User granted access.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// When this permission was granted.
    /// </summary>
    public DateTimeOffset GrantedAt { get; set; }

    /// <summary>
    /// Who granted this permission (audit trail).
    /// Null = system/migration.
    /// </summary>
    public Guid? GrantedByUserId { get; set; }

    /// <summary>
    /// When this permission expires (optional, RFC-034).
    /// Null = no expiration.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>
    /// Optional reason for this grant (audit trail).
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// When this permission was revoked (soft-delete, ADR-0011).
    /// Null = active.
    /// Revocation is blocker audit (AuditLog entry in same transaction, ADR-0007).
    /// After revocation, user cannot launch new sessions (RF-009).
    /// Existing sessions are NOT terminated without RF-008 (R-014).
    /// </summary>
    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>
    /// Who revoked this permission (audit trail).
    /// </summary>
    public Guid? RevokedByUserId { get; set; }

    /// <summary>
    /// Reason for revocation (audit trail).
    /// </summary>
    public string? RevocationReason { get; set; }

    /// <summary>
    /// Last update timestamp (UTC).
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation properties
    public Tenant Tenant { get; set; } = null!;
    public Application Application { get; set; } = null!;
    public User User { get; set; } = null!;
}
