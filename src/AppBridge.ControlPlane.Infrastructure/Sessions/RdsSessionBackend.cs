using AppBridge.ControlPlane.Domain.Catalog;
using AppBridge.ControlPlane.Domain.Sessions;
using AppBridge.ControlPlane.Infrastructure.Rdp;
using Microsoft.EntityFrameworkCore;

namespace AppBridge.ControlPlane.Infrastructure.Sessions;

/// <summary>
/// See <see cref="ISessionBackend"/>. "Rds" in the name is the whole point — every other component
/// in the system is meant to be blind to it (RNF-035). Host and application data both already live
/// in our own database (T-202/T-204) — "resolução de host e descritor" (T-503's exact scope) reads
/// that data, it does not call out to a real RD Connection Broker. That live-broker dependency
/// belongs to the members this interface doesn't have yet (<c>ListActiveSessionsAsync</c> and
/// friends) — a correction made before writing this class, not after: an earlier message in this
/// session assumed T-503 needed a fake the way T-502's <c>rdpsign.exe</c> dependency did, and that
/// assumption doesn't hold for the scope this task actually has.
/// </summary>
public sealed class RdsSessionBackend(AppBridgeDbContext dbContext) : ISessionBackend
{
    public async Task<SessionHost?> ResolveHostAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        var application = await dbContext.Applications.SingleOrDefaultAsync(a => a.Id == applicationId, cancellationToken);
        if (application is null)
        {
            return null;
        }

        // UUID v7 ids sort by creation time (ADR-0011) — ordering by Id gives a deterministic,
        // reproducible pick without needing real load data. Actual load balancing across hosts is
        // a later concern (GetHostHealthAsync, V2); MVP-0 dogfood runs a single host pool anyway.
        return await dbContext.SessionHosts
            .Where(h => h.HostPoolId == application.HostPoolId && h.Status == SessionHostStatus.Online)
            .OrderBy(h => h.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<RdpConnectionParameters> BuildConnectionDescriptorAsync(
        SessionHost host, Application application, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new RdpConnectionParameters(
            HostAddress: host.Fqdn,
            RemoteAppAlias: application.RemoteAppAlias,
            RemoteAppDisplayName: application.DisplayName));
    }
}
