using AppBridge.ControlPlane.Infrastructure;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AppBridge.ControlPlane.Api.HealthChecks;

/// <summary>
/// The first real dependency check the health endpoint gets (T-301) — AppBridgeDbContext now has an
/// actual runtime consumer (POST /v1/auth/session), so this is a check with something behind it,
/// not a stub built ahead of the code that would back it (T-201's own reasoning). Hand-rolled rather
/// than a third-party health-check package — <c>Database.CanConnectAsync()</c> is all this needs.
/// </summary>
public sealed class PostgresHealthCheck(AppBridgeDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        return await dbContext.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy();
    }
}
