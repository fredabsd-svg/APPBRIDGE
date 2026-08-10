using AppBridge.ControlPlane.Domain.Common;

namespace AppBridge.ControlPlane.Domain.Sessions;

/// <summary>MODELO-DE-DADOS.md §6.1.</summary>
public sealed class HostPool : TenantScopedEntity
{
    public required string Name { get; set; }

    /// <summary>
    /// Today this is always Rds — no AvdSessionBackend exists yet (R-018). The column exists so a
    /// pool created now doesn't need a later migration to say what backend serves it.
    /// </summary>
    public BackendType BackendType { get; set; } = BackendType.Rds;
}
