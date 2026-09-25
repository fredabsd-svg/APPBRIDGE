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

/// <summary>Grant persistente que sustenta access tokens curtos de uma estação.</summary>
public sealed class AuthenticationSession : TenantMutableEntity
{
    public Guid UserAccountId { get; set; }
    public string WorkstationName { get; set; } = string.Empty;
    public DateTimeOffset LastUsedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset AbsoluteExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? RevocationReason { get; set; }
}

/// <summary>Hash de um refresh token emitido; registros consumidos ficam para detectar replay.</summary>
public sealed class AuthenticationRefreshToken : TenantMutableEntity
{
    public Guid SessionId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
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
