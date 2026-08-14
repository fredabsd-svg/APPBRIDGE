using AppBridge.ControlPlane.Domain.Sessions;
using AppBridge.ControlPlane.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace AppBridge.ControlPlane.Infrastructure.Sessions;

/// <summary>See <see cref="ISessionReconciler"/>.</summary>
public sealed class SessionReconciler(
    AppBridgeDbContext dbContext, ISessionBackend sessionBackend, TenantContext tenantContext, SessionReconcilerOptions options)
    : ISessionReconciler
{
    public async Task<int> ReconcileStaleSessionsAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = DateTimeOffset.UtcNow - options.InactivityWindow;

        // A background sweep has no single ambient tenant — the same explicit IgnoreQueryFilters()
        // reasoning AuthEndpoints.FindRefreshTokenAsync already established (ADR-0004 item 7): there
        // is no tenant to scope by until each row tells us which one it belongs to. Projected, not
        // tracked — CancelSessionAsync below does its own fetch, scoped by tenant.
        var stale = await dbContext.Sessions
            .IgnoreQueryFilters()
            .Where(s => s.EndedAt == null && s.LastSeenAt < cutoff)
            .Select(s => new { s.Id, s.TenantId })
            .ToListAsync(cancellationToken);

        foreach (var session in stale)
        {
            // CancelSessionAsync's own lookup is tenant-filtered — the ambient tenant has to match
            // the session being closed for each iteration, since this sweep spans every tenant.
            tenantContext.TenantId = session.TenantId;
            await sessionBackend.CancelSessionAsync(session.Id, SessionEndReason.StaleExpired, cancellationToken);
        }

        return stale.Count;
    }
}
