namespace AppBridge.ControlPlane.Core.Entities;

/// <summary>
/// RemoteApp application published via the Control Plane.
/// Contains metadata for the launcher and RDP file generation.
/// Multi-tenant: explicitly bound to Tenant (ADR-0004).
/// </summary>
public class Application
{
    public Guid Id { get; set; }

    /// <summary>
    /// Tenant ID (foreign key + isolation boundary).
    /// Part of composite unique key (tenant_id, identifier).
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Unique identifier within the tenant (e.g., "dominio", "alterdata").
    /// </summary>
    public string Identifier { get; set; } = null!;

    /// <summary>
    /// Display name shown in launcher (RF-010).
    /// </summary>
    public string DisplayName { get; set; } = null!;

    /// <summary>
    /// Optional description shown in launcher.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// RemoteApp program name on session hosts (e.g., "dominio.rdp").
    /// </summary>
    public string RemoteAppName { get; set; } = null!;

    /// <summary>
    /// Optional icon URL or embedded data (RF-012).
    /// Served by endpoint with ETag caching.
    /// </summary>
    public string? IconUrl { get; set; }

    /// <summary>
    /// Whether this application is published to users (RF-040).
    /// </summary>
    public bool IsPublished { get; set; } = true;

    /// <summary>
    /// Current license count being tracked (RF-062, ADR-0006).
    /// Incremented on session start, decremented on session end.
    /// </summary>
    public int CurrentLicenseCount { get; set; } = 0;

    /// <summary>
    /// Maximum concurrent sessions allowed (RF-062, ADR-0006).
    /// 0 = unlimited.
    /// </summary>
    public int MaxConcurrentSessions { get; set; } = 0;

    /// <summary>
    /// Whether to record session logs (part of audit trail, ADR-0007).
    /// </summary>
    public bool LogSessionActivity { get; set; } = true;

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
    public ICollection<ApplicationUserPermission> UserPermissions { get; set; } = new List<ApplicationUserPermission>();
}
