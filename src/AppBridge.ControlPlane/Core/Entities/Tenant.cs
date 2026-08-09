namespace AppBridge.ControlPlane.Core.Entities;

/// <summary>
/// Multi-tenant organization/customer entity.
/// Core isolation boundary (RNF-036, ADR-0004).
/// </summary>
public class Tenant
{
    public Guid Id { get; set; }

    /// <summary>
    /// Unique identifier for the tenant (subdomain or organization code).
    /// </summary>
    public string Identifier { get; set; } = null!;

    /// <summary>
    /// Display name of the tenant.
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Optional description of the tenant.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Whether this tenant is active. Soft-delete replacement for MVP-0.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Creation timestamp (UTC). First audit field (ADR-0011).
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
    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<Application> Applications { get; set; } = new List<Application>();
    public ICollection<Session> Sessions { get; set; } = new List<Session>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}
