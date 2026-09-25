using AppBridge.ControlPlane.Data;
using AppBridge.ControlPlane.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AppBridge.ControlPlane.Services;

public sealed class LaunchMeteringService(AppDbContext dbContext)
{
    public Task<long> CountUserInitiatedAsync(
        Guid applicationId,
        DateTimeOffset fromInclusive,
        DateTimeOffset toExclusive,
        CancellationToken cancellationToken = default)
        => dbContext.Launches.LongCountAsync(launch =>
            launch.ApplicationId == applicationId
            && launch.Purpose == LaunchPurpose.UserInitiated
            && launch.RequestedAt >= fromInclusive
            && launch.RequestedAt < toExclusive,
            cancellationToken);
}
