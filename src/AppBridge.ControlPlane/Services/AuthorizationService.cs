using AppBridge.ControlPlane.Data;
using AppBridge.ControlPlane.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AppBridge.ControlPlane.Services;

public sealed class AuthorizationService(AppDbContext dbContext)
{
    public async Task<RemoteApplication?> GetAuthorizedApplicationAsync(
        Guid userAccountId,
        Guid applicationId,
        CancellationToken cancellationToken = default)
    {
        if (!await CanLaunchAsync(userAccountId, applicationId, cancellationToken: cancellationToken))
        {
            return null;
        }

        return await dbContext.Applications.SingleOrDefaultAsync(application =>
            application.Id == applicationId
            && application.Status == Domain.Enums.ApplicationStatus.Published,
            cancellationToken);
    }

    public async Task<bool> CanLaunchAsync(
        Guid userAccountId,
        Guid applicationId,
        DateTimeOffset? at = null,
        CancellationToken cancellationToken = default)
    {
        var now = at ?? DateTimeOffset.UtcNow;
        if (!await dbContext.UserAccounts.AnyAsync(user =>
                user.Id == userAccountId && user.Status == Domain.Enums.UserAccountStatus.Active,
                cancellationToken))
        {
            return false;
        }

        return await dbContext.ApplicationPermissions
            .Join(dbContext.UserGroupMemberships,
                permission => new { permission.TenantId, permission.GroupId },
                membership => new { membership.TenantId, membership.GroupId },
                (permission, membership) => new { permission, membership })
            .AnyAsync(x => x.membership.UserAccountId == userAccountId
                && x.permission.ApplicationId == applicationId
                && x.permission.EffectiveFrom <= now
                && (x.permission.EffectiveTo == null || x.permission.EffectiveTo > now), cancellationToken);
    }

    public async Task<IReadOnlyList<RemoteApplication>> GetApplicationsAsync(
        Guid userAccountId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        if (!await dbContext.UserAccounts.AnyAsync(user =>
                user.Id == userAccountId && user.Status == Domain.Enums.UserAccountStatus.Active,
                cancellationToken))
        {
            return Array.Empty<RemoteApplication>();
        }

        var authorizedIds = dbContext.ApplicationPermissions
            .Join(dbContext.UserGroupMemberships,
                permission => new { permission.TenantId, permission.GroupId },
                membership => new { membership.TenantId, membership.GroupId },
                (permission, membership) => new { permission, membership })
            .Where(x => x.membership.UserAccountId == userAccountId
                && x.permission.EffectiveFrom <= now
                && (x.permission.EffectiveTo == null || x.permission.EffectiveTo > now))
            .Select(x => x.permission.ApplicationId);

        return await dbContext.Applications
            .Where(application => application.Status == Domain.Enums.ApplicationStatus.Published
                && authorizedIds.Contains(application.Id))
            .OrderBy(application => application.DisplayName)
            .ThenBy(application => application.Id)
            .ToListAsync(cancellationToken);
    }
}
