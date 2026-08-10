namespace AppBridge.ControlPlane.Domain.Trail;

/// <summary>
/// ADR-0016, Gap 1. Without this distinction, metering (RF-062) would count prelaunches as real
/// use and the cap (RF-064) would block legitimate work.
/// </summary>
public enum LaunchPurpose
{
    UserInitiated,
    Prelaunch,
}
