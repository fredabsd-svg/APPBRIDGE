namespace AppBridge.ControlPlane.Infrastructure.Auditing;

/// <summary>
/// Thrown by <see cref="IAuditWriter"/> when the audit record — and therefore the operation it
/// accompanies — could not be committed (ADR-0007 Part 1). Its own type, distinct from a bare
/// <c>DbUpdateException</c>, is what lets an endpoint (T-301 onward) catch specifically this
/// failure mode and answer with the stable <c>503 AUDIT_UNAVAILABLE</c> Problem Details response
/// (API.md, ADR-0012) instead of a generic 500 — condition 1's "mensagem acionável ... que não
/// expõe detalhe interno" starts here, not at the controller.
/// </summary>
public sealed class AuditWriteFailedException(string message, Exception innerException)
    : Exception(message, innerException);
