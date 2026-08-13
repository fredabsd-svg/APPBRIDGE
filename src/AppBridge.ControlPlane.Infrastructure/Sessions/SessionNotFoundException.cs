namespace AppBridge.ControlPlane.Infrastructure.Sessions;

/// <summary>
/// Thrown by <see cref="ISessionBackend.CancelSessionAsync"/> when the given session id does not
/// exist for the current tenant (ADR-0004 — a session belonging to another tenant is indistinguishable
/// from one that does not exist at all).
/// </summary>
public sealed class SessionNotFoundException(Guid sessionId)
    : Exception($"Session {sessionId} was not found for the current tenant.");
