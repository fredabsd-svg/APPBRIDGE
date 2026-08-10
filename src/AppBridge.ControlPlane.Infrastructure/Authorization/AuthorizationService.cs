using Microsoft.EntityFrameworkCore;

namespace AppBridge.ControlPlane.Infrastructure.Authorization;

/// <summary>See <see cref="IAuthorizationService"/>.</summary>
public sealed class AuthorizationService(AppBridgeDbContext dbContext) : IAuthorizationService
{
    public async Task<bool> HasActivePermissionAsync(
        Guid userAccountId, Guid applicationId, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        return await (
            from membership in dbContext.UserGroupMemberships
            join permission in dbContext.ApplicationPermissions
                on membership.GroupId equals permission.GroupId
            where membership.UserAccountId == userAccountId
                && permission.ApplicationId == applicationId
                && permission.EffectiveFrom <= now
                && (permission.EffectiveTo == null || permission.EffectiveTo > now)
            select permission.Id
        ).AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlySet<Guid>> GetAuthorizedApplicationIdsAsync(
        Guid userAccountId, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        var applicationIds = await (
            from membership in dbContext.UserGroupMemberships
            join permission in dbContext.ApplicationPermissions
                on membership.GroupId equals permission.GroupId
            where membership.UserAccountId == userAccountId
                && permission.EffectiveFrom <= now
                && (permission.EffectiveTo == null || permission.EffectiveTo > now)
            select permission.ApplicationId
        ).Distinct().ToListAsync(cancellationToken);

        return applicationIds.ToHashSet();
    }
}
