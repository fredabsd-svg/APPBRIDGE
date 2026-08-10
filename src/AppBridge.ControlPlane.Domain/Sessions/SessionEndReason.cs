namespace AppBridge.ControlPlane.Domain.Sessions;

public enum SessionEndReason
{
    Logoff,
    DisconnectTimeout,
    TerminatedByAdmin,
    Revoked,

    /// <summary>
    /// The Connection Broker no longer lists this session but the Control Plane never observed the
    /// end — the primary defence against a licence counter that inflates forever (R-009).
    /// </summary>
    ReconciledMissing,

    /// <summary>No heartbeat within the inactivity window — the secondary defence against R-009.</summary>
    StaleExpired,
}
