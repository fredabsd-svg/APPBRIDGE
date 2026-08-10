namespace AppBridge.ControlPlane.Infrastructure.Tenancy;

/// <summary>
/// The tenant that feeds the global query filter (ADR-0004 item 6) for the current unit of work.
/// Resolved from the request's token — this interface has no opinion on how; T-301's auth
/// middleware sets it early in the pipeline. The filter reads this, never a caller-supplied
/// parameter, so a query that forgets to scope itself stays isolated regardless.
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// Null until something resolves and sets it (no request yet reached the auth middleware, a
    /// background job running outside a tenant, a design-time tool). A query filter fed by a null
    /// tenant returns zero rows, not every tenant's rows — isolation has to fail closed.
    /// </summary>
    Guid? TenantId { get; }
}
