using AppBridge.ControlPlane.Domain.Catalog;
using AppBridge.ControlPlane.Domain.Sessions;
using Microsoft.EntityFrameworkCore;

namespace AppBridge.ControlPlane.Infrastructure.Sessions;

/// <summary>See <see cref="ISessionRegistry"/>.</summary>
public sealed class SessionRegistry(AppBridgeDbContext dbContext) : ISessionRegistry
{
    public async Task<SessionRegistration> RegisterAsync(
        Guid tenantId, Guid userAccountId, SessionHost host, string? sourceIp, string? workstationName,
        CancellationToken cancellationToken = default)
    {
        var active = await dbContext.Sessions.SingleOrDefaultAsync(
            s => s.SessionHostId == host.Id && s.UserAccountId == userAccountId && s.EndedAt == null,
            cancellationToken);

        if (active is not null)
        {
            active.LastSeenAt = DateTimeOffset.UtcNow;
            return new SessionRegistration(active.Id, Reused: true);
        }

        var now = DateTimeOffset.UtcNow;
        var session = new Session
        {
            TenantId = tenantId,
            UserAccountId = userAccountId,
            SessionHostId = host.Id,
            // PREMISSA (T-601): the real RDS-side identifier is only knowable once something talks
            // to the Connection Broker — no member of ISessionBackend does that yet (T-503's own
            // scope is deliberately limited to reading our own tables), and this table's own FK to
            // Launch (Launch.SessionId) already gives full traceability back to the exact request
            // that created this row. A placeholder makes the gap self-evident to whoever reads the
            // row — including SessionReconciler (T-602), the component expected to replace it once
            // it can actually observe the broker.
            BackendSessionId = $"pending:{Guid.NewGuid()}",
            StartedAt = now,
            LastSeenAt = now,
            SourceIp = sourceIp,
            WorkstationName = workstationName,
        };
        dbContext.Sessions.Add(session);
        return new SessionRegistration(session.Id, Reused: false);
    }
}
