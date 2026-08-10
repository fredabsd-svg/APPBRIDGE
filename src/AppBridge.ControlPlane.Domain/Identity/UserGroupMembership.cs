using AppBridge.ControlPlane.Domain.Common;

namespace AppBridge.ControlPlane.Domain.Identity;

/// <summary>
/// MODELO-DE-DADOS.md §4.2. For directory groups this table is a cache — the directory,
/// re-queried at login (RF-010), is the source of truth. <see cref="SyncedAt"/> exists so an
/// authorization decision never rests on a stale cache without that being detectable.
/// </summary>
public sealed class UserGroupMembership : TenantScopedEntity
{
    /// <summary>Composite FK <c>fk_user_group_membership_user_account</c> (ADR-0011 §4, T-204).</summary>
    public Guid UserAccountId { get; set; }

    /// <summary>Composite FK <c>fk_user_group_membership_group</c> (ADR-0011 §4, T-204).</summary>
    public Guid GroupId { get; set; }

    public GroupSource Source { get; set; } = GroupSource.Local;

    public DateTimeOffset? SyncedAt { get; set; }
}
