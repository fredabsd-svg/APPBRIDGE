using AppBridge.ControlPlane.Domain.Common;

namespace AppBridge.ControlPlane.Domain.Catalog;

/// <summary>MODELO-DE-DADOS.md §5.1.</summary>
public sealed class Application : TenantScopedEntity
{
    public required string DisplayName { get; set; }

    public string? Description { get; set; }

    /// <summary>Reference to the icon file; the binary itself lives outside the database (API.md §3).</summary>
    public string? IconRef { get; set; }

    public required string RemoteAppAlias { get; set; }

    // TODO(T-204): ADR-0011 §4 composite FK (tenant_id, host_pool_id) -> host_pool(tenant_id, id).
    public Guid HostPoolId { get; set; }

    public LaunchMode LaunchMode { get; set; } = LaunchMode.RemoteApp;

    public ApplicationStatus Status { get; set; } = ApplicationStatus.Draft;

    /// <summary>Concurrent-use cap (RF-063, MVP-1). Null means unlimited.</summary>
    public int? ConcurrentLimit { get; set; }

    /// <summary>Licence declared by the client — titularity is theirs, not ours (ADR-0014). Feeds T-001.</summary>
    public string? LicenseNotes { get; set; }
}
