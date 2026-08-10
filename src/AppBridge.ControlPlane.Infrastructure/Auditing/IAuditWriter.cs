namespace AppBridge.ControlPlane.Infrastructure.Auditing;

/// <summary>
/// The single path a security-relevant operation (RF-036, RF-037, RF-039, RF-041, RF-042) goes
/// through to make its audit record durable. ADR-0007 Part 1: "a gravação do registro de auditoria
/// faz parte da mesma transação que concede o acesso" — this interface exists so that guarantee is
/// a property of the mechanism, not something every future caller has to remember to wire up
/// correctly (the same reasoning ADR-0004 applied to the tenant filter, T-203).
///
/// Does NOT apply to telemetry, metrics, the launcher's diagnostic log, or session end (RF-038) —
/// ADR-0007 excludes those explicitly. Callers for those write directly through the
/// <c>DbContext</c>/logger, not through this interface.
/// </summary>
public interface IAuditWriter
{
    /// <summary>
    /// Adds <paramref name="auditEntry"/> (an <c>AccessEvent</c>, a <c>Launch</c>, or any other
    /// append-only trail entity) and runs <paramref name="grant"/> against the same
    /// <c>AppBridgeDbContext</c>, then commits both in a single <c>SaveChangesAsync</c>. If the
    /// commit fails for any reason — the audit insert, whatever <paramref name="grant"/> changed,
    /// or the database itself — nothing persists and <see cref="AuditWriteFailedException"/> is
    /// thrown: "falha de gravação nega a operação", not just the audit half of it.
    ///
    /// <paramref name="grant"/> is synchronous and DB-only on purpose: a side effect with real I/O
    /// (signing an .rdp file, calling <c>ISessionBackend</c>) belongs *after* a successful commit,
    /// never inside this transactional boundary — the signature itself is what rules that out,
    /// not a comment a future caller could miss.
    /// </summary>
    Task<TResult> ExecuteAsync<TAudit, TResult>(
        TAudit auditEntry,
        Func<AppBridgeDbContext, TResult> grant,
        CancellationToken cancellationToken = default)
        where TAudit : class;

    /// <summary>Same guarantee as the generic overload, for callers with no result to return.</summary>
    Task ExecuteAsync<TAudit>(
        TAudit auditEntry,
        Action<AppBridgeDbContext> grant,
        CancellationToken cancellationToken = default)
        where TAudit : class;
}
