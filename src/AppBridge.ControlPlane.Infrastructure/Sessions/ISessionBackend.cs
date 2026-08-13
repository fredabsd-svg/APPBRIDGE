using AppBridge.ControlPlane.Domain.Catalog;
using AppBridge.ControlPlane.Domain.Sessions;
using AppBridge.ControlPlane.Infrastructure.Rdp;

namespace AppBridge.ControlPlane.Infrastructure.Sessions;

/// <summary>
/// RNF-035 / ARQUITETURA.md §4.2: the portability boundary — no business rule anywhere else in the
/// Control Plane is allowed to know it's RDS underneath. Trading <c>RdsSessionBackend</c> for a
/// future <c>AvdSessionBackend</c> (RM-07) is meant to be a new implementation of this interface,
/// not a rewrite of anything that calls it.
///
/// T-503 built only the two members its own acceptance criterion needed ("resolução de host e
/// descritor"). T-506 adds <see cref="CancelSessionAsync"/> (ADR-0016, Gap 2). ARQUITETURA.md's
/// component table lists further members (<c>ListActiveSessionsAsync</c>, <c>TerminateSessionAsync</c>,
/// <c>PublishApplicationAsync</c>, <c>GetHostHealthAsync</c>) — each belongs to a task that hasn't
/// started yet (T-601/602, MVP-1, V2 respectively) and gets added to this interface when that task
/// actually needs it, the same way T-402 added <c>IAuthorizationService.GetAuthorizedApplicationIdsAsync</c>
/// to a contract T-304 had deliberately kept minimal. Stub methods for work months away would be
/// scope invented ahead of the requirement that justifies it (RP-05).
/// </summary>
public interface ISessionBackend
{
    /// <summary>
    /// Picks the <see cref="SessionHost"/> that will serve a launch of <paramref name="applicationId"/>
    /// — an <see cref="SessionHostStatus.Online"/> host in that application's <c>HostPool</c>. Null
    /// means no eligible host exists (maps to <c>422 APPLICATION_UNAVAILABLE</c>, API.md §4 — that
    /// mapping itself belongs to T-504, not built here). Tenant isolation is automatic (ADR-0004):
    /// no tenant parameter, the same shape as <c>IAuthorizationService</c>.
    /// </summary>
    Task<SessionHost?> ResolveHostAsync(Guid applicationId, CancellationToken cancellationToken = default);

    /// <summary>Produces exactly what <see cref="IRdpDescriptorBuilder.Build"/> (T-501) consumes — this is the seam ARQUITETURA.md's "os parâmetros de conexão que viram o .rdp" describes.</summary>
    Task<RdpConnectionParameters> BuildConnectionDescriptorAsync(
        SessionHost host, Application application, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a <see cref="Session"/> ended with <paramref name="reason"/> — ADR-0016 Gap 2: without
    /// this operation, a prelaunch that fails after a session already exists on the RDS side leaves
    /// a "zombie" that keeps counting against the licence ceiling (ADR-0006) until it expires by
    /// inactivity (<see cref="SessionEndReason.StaleExpired"/>). Idempotent: cancelling an
    /// already-ended session is a no-op, not an error — two callers racing to close the same session
    /// (this operation and <c>SessionReconciler</c>, T-602) must not throw on the loser.
    /// Not routed through <c>IAuditWriter</c> — session end (RF-038) is explicitly outside that
    /// interface's scope (see its doc comment); <see cref="Session.EndedAt"/>/<see cref="Session.EndReason"/>
    /// is itself the durable record. Throws <see cref="SessionNotFoundException"/> if
    /// <paramref name="sessionId"/> does not exist for the current tenant.
    /// </summary>
    Task CancelSessionAsync(Guid sessionId, SessionEndReason reason, CancellationToken cancellationToken = default);
}
