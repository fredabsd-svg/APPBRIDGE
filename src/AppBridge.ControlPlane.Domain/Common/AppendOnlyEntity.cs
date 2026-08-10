namespace AppBridge.ControlPlane.Domain.Common;

/// <summary>
/// Base for trail tables (launch, access_event, purge_run). Deliberately carries only
/// <see cref="Id"/>, <see cref="CreatedAt"/> and <see cref="CreatedBy"/> — no updated/deleted
/// columns exist at all, on any of them (ADR-0011 §5). A trail row that admits an "updated by"
/// column is a row that admits alteration, which is exactly what RNF-019 forbids. The absence is
/// the control, not an oversight to fill in later.
/// </summary>
public abstract class AppendOnlyEntity
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public DateTimeOffset CreatedAt { get; init; }

    public Guid? CreatedBy { get; init; }
}
