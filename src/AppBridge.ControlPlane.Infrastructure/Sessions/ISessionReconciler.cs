using AppBridge.ControlPlane.Domain.Sessions;

namespace AppBridge.ControlPlane.Infrastructure.Sessions;

/// <summary>
/// T-602 (ADR-0016 Gap 2, second half; R-009; RF-038). Closes sessions this table still shows as
/// active but that nothing has touched in a long time — the <c>stale_expired</c> defence
/// (MODELO-DE-DADOS.md §6.2) against a licence/audit counter that grows forever.
///
/// This is deliberately only half of what ROADMAP.md's T-602 row describes. The other half —
/// <c>reconciled_missing</c>, discovering a session the real RD Connection Broker no longer lists —
/// requires periodically querying that broker. ADR-0006 already decided that query belongs to
/// <b>MVP-1</b>, not MVP-0a: "Essa consulta passa a ser parte do escopo do MVP-1 e deve ficar atrás
/// da interface de backend de sessão" — and its alternatives-considered table explicitly rejected
/// anteceding it to MVP-0 for calendar-risk reasons. Building it now, even behind a fake the way
/// T-502 stood in for <c>rdpsign.exe</c>, would mean shipping code ahead of a phase boundary an
/// accepted ADR already set — that needs a new ADR superseding ADR-0006, not a unilateral call made
/// while implementing an unrelated task. <c>ISessionBackend</c> is left without a
/// <c>ListActiveSessionsAsync</c>/broker-querying member for the same reason T-503 left it without
/// members no acceptance criterion needed yet.
///
/// <c>stale_expired</c> itself has no such blocker: it only reads <see cref="Session.LastSeenAt"/>,
/// a column this codebase already owns, against a configured inactivity window — no RDS dependency,
/// nothing ADR-0006 touches.
/// </summary>
public interface ISessionReconciler
{
    /// <summary>Closes every active session across every tenant whose <c>LastSeenAt</c> is older than the configured window. Returns how many were closed.</summary>
    Task<int> ReconcileStaleSessionsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// <c>PREMISSA</c> (STATUS.md PRE-32): 12 hours — comfortably longer than a single workday (PRE-22),
/// so a session with no <c>LastSeenAt</c> update because its user simply kept working in the one app
/// they launched (no second launch to trigger <c>SessionRegistry</c>'s reuse touch, T-601 — there is
/// no heartbeat mechanism yet) is not closed out from under them. Coarse on purpose: in MVP-0a,
/// closing a session early has no user-facing consequence — quota enforcement (RF-064) that would
/// actually block someone is MVP-1 (ADR-0006) — the only cost of a wrong guess here is bookkeeping
/// imprecision, not blocked work. Needs measurement in the dogfood, same as PRE-22.
/// </summary>
public sealed class SessionReconcilerOptions
{
    public TimeSpan InactivityWindow { get; init; } = TimeSpan.FromHours(12);
}
