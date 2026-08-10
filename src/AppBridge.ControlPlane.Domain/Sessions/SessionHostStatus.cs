namespace AppBridge.ControlPlane.Domain.Sessions;

public enum SessionHostStatus
{
    Online,

    /// <summary>Blocked from new launches during maintenance (RF-068, V3) — modelled from MVP-0.</summary>
    Draining,

    Offline,
}
