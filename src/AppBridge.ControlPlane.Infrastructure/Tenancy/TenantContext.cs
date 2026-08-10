namespace AppBridge.ControlPlane.Infrastructure.Tenancy;

/// <summary>
/// Scoped, settable holder for the resolved tenant. Registered per-request in DI; T-301's auth
/// middleware sets <see cref="TenantId"/> once, before any query runs. Tests set it directly — no
/// HTTP pipeline required to exercise the filter it feeds.
/// </summary>
public sealed class TenantContext : ITenantContext
{
    public Guid? TenantId { get; set; }
}
