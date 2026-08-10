namespace AppBridge.ControlPlane.Core.Entities;

/// <summary>
/// User entity representing a person with access to applications in the tenant.
/// Multi-tenant: explicitly bound to Tenant (ADR-0004, ADR-0011).
/// </summary>
public class User
{
    public Guid Id { get; set; }

    /// <summary>
    /// Tenant ID (foreign key + isolation boundary).
    /// Part of composite unique key (tenant_id, identifier).
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// User identifier in AD/Entra ID (UPN or email).
    /// Unique per tenant (ADR-0001).
    /// </summary>
    public string Identifier { get; set; } = null!;

    /// <summary>
    /// Display name (from AD/Entra ID).
    /// </summary>
    public string DisplayName { get; set; } = null!;

    /// <summary>
    /// Email address (from AD/Entra ID).
    /// </summary>
    public string Email { get; set; } = null!;

    /// <summary>
    /// Hashed password (bcrypt). Required for local authentication in MVP-0.
    /// Future: replace with Entra ID/AD integration.
    /// </summary>
    public string PasswordHash { get; set; } = null!;

    /// <summary>
    /// Whether this user is currently active (can launch apps).
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Last successful login timestamp (UTC).
    /// </summary>
    public DateTimeOffset? LastLoginAt { get; set; }

    /// <summary>
    /// Creation timestamp (UTC).
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Last update timestamp (UTC).
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Soft-delete marker. Null = not deleted (ADR-0011).
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }

    // Navigation properties
    public Tenant Tenant { get; set; } = null!;
    public ICollection<Session> Sessions { get; set; } = new List<Session>();
    public ICollection<ApplicationUserPermission> ApplicationPermissions { get; set; } = new List<ApplicationUserPermission>();
}
