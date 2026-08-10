namespace AppBridge.ControlPlane.Domain.Sessions;

/// <summary>The ISessionBackend implementation a pool is served by (RNF-035, ARQUITETURA.md §4.2).</summary>
public enum BackendType
{
    Rds,
    Avd,
}
