using AppBridge.ControlPlane.Domain.Common;
using AppBridge.ControlPlane.Domain.Tenancy;

namespace AppBridge.ControlPlane.Domain.Trail;

/// <summary>
/// MODELO-DE-DADOS.md §7.5. If the purge left no trace, the trail would have a mechanism capable
/// of erasing evidence without a record — which would undermine RNF-019 from the inside. This
/// table is never purged itself.
/// </summary>
public sealed class PurgeRun : TenantScopedAppendOnlyEntity
{
    public RetentionCategory Category { get; set; }

    public DateTimeOffset CutoffDate { get; set; }

    public int RowsDeleted { get; set; }

    public DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset? FinishedAt { get; set; }

    /// <summary>
    /// Free text, not a closed enum: MODELO-DE-DADOS.md §7.5 does not enumerate outcome values.
    /// </summary>
    public required string Outcome { get; set; }
}
