using AppBridge.ControlPlane.Domain.Common;

namespace AppBridge.ControlPlane.Domain.Identity;

/// <summary>MODELO-DE-DADOS.md §4.2. Permission always targets a group (RF-010), never a user directly.</summary>
public sealed class Group : TenantScopedEntity
{
    public required string Name { get; set; }

    public GroupSource Source { get; set; } = GroupSource.Local;

    /// <summary>Null when <see cref="Source"/> is Local.</summary>
    public string? ExternalGroupId { get; set; }
}
