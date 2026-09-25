using AppBridge.ControlPlane.Domain.Enums;

namespace AppBridge.ControlPlane.Domain.Entities;

public sealed class UserAccount : TenantMutableEntity
{
    public string ExternalSubject { get; set; } = string.Empty;
    public string Upn { get; set; } = string.Empty;
    public string AdObjectSid { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public UserAccountStatus Status { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
}

public sealed class AppGroup : TenantMutableEntity
{
    public string Name { get; set; } = string.Empty;
    public GroupSource Source { get; set; }
    public string? ExternalGroupId { get; set; }
}

public sealed class UserGroupMembership : TenantMutableEntity
{
    public Guid UserAccountId { get; set; }
    public Guid GroupId { get; set; }
    public GroupSource Source { get; set; }
    public DateTimeOffset? SyncedAt { get; set; }
}
