using AppBridge.ControlPlane.Domain.Enums;

namespace AppBridge.ControlPlane.Domain.Entities;

public sealed class RemoteApplication : TenantMutableEntity
{
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string IconRef { get; set; } = string.Empty;
    public string RemoteAppAlias { get; set; } = string.Empty;
    public Guid HostPoolId { get; set; }
    public ApplicationLaunchMode LaunchMode { get; set; }
    public ApplicationStatus Status { get; set; }
    public int? ConcurrentLimit { get; set; }
    public string? LicenseNotes { get; set; }
}

public sealed class ApplicationPermission : TenantVersionedEntity
{
    public Guid ApplicationId { get; set; }
    public Guid GroupId { get; set; }
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public Guid GrantedBy { get; set; }
    public Guid? RevokedBy { get; set; }
    public string? RevocationReason { get; set; }
}
