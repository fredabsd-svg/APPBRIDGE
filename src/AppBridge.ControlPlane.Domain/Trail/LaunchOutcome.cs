namespace AppBridge.ControlPlane.Domain.Trail;

/// <summary>MODELO-DE-DADOS.md §7.1 — closed set, as documented.</summary>
public enum LaunchOutcome
{
    Granted,
    DeniedPermission,
    DeniedQuota,
    DeniedHostUnavailable,
    ErrorSigning,
    ErrorInternal,
}
