using AppBridge.ControlPlane.Domain.Catalog;
using AppBridge.ControlPlane.Domain.Sessions;

namespace AppBridge.ControlPlane.Infrastructure.Sessions;

/// <summary>
/// T-601 (ARQUITETURA.md §5.2 step "SessionRegistry + auditoria do lançamento"; RF-024). The CP's
/// own bookkeeping of session lifetime — deliberately separate from <see cref="ISessionBackend"/>:
/// this never talks to RDS (RNF-035's boundary), it only reads/writes the <c>session</c> table this
/// codebase already owns (T-204), the same category of work <c>IAuthorizationService</c> does over
/// <c>application_permission</c>.
///
/// <see cref="RegisterAsync"/> only reads and stages tracked changes — it never calls
/// <c>SaveChangesAsync</c> itself. Called from <c>LaunchEndpoints</c> against the exact same
/// <c>AppBridgeDbContext</c> instance <c>IAuditWriter</c> commits moments later, so the staged
/// <see cref="Session"/> change and the granted <c>Launch</c> row land in one transaction — the
/// same "single <c>SaveChangesAsync</c>, all or nothing" guarantee <c>IAuditWriter</c> already
/// gives the audit row, extended for free to whatever else is tracked on that context by the time
/// it commits. That, not a compensating cancel call, is what rules out a "session written but
/// launch failed" zombie: there is no commit in between the two for anything to fail after.
///
/// The scenario ADR-0016 Gap 2 actually describes — a session that exists in this table but was
/// never really established on the RDS side, because the client-side <c>mstsc</c> connection
/// (ARQUITETURA.md §5.2, which only happens after the Control Plane's response) failed silently —
/// cannot be observed synchronously by anything in this request: API.md is explicit that the
/// client is never trusted to report session end. Closing that gap is <c>SessionReconciler</c>'s
/// job (T-602, against the real Connection Broker), which is also the only caller
/// <c>ISessionBackend.CancelSessionAsync</c> (T-506) ends up needing — see ROADMAP.md's T-601/T-602
/// note for the fuller reasoning, including why an earlier note (written at the end of T-506)
/// assigned that wiring to this task instead.
/// </summary>
public interface ISessionRegistry
{
    /// <summary>
    /// Reuses the caller's active session on <paramref name="host"/> if one exists (RF-024),
    /// bumping <see cref="Session.LastSeenAt"/>; otherwise stages a new one. Tenant isolation is
    /// automatic (ADR-0004) for the reuse lookup; <paramref name="tenantId"/> is only needed to
    /// stamp a freshly staged <see cref="Session"/>, the same reason <c>LaunchEndpoints.NewLaunch</c>
    /// takes it explicitly.
    /// </summary>
    Task<SessionRegistration> RegisterAsync(
        Guid tenantId, Guid userAccountId, SessionHost host, string? sourceIp, string? workstationName,
        CancellationToken cancellationToken = default);
}

/// <summary><paramref name="Reused"/> is exactly RF-024's "sessionReused" (API.md §4).</summary>
public sealed record SessionRegistration(Guid SessionId, bool Reused);
