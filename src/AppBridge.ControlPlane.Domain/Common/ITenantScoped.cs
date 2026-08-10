namespace AppBridge.ControlPlane.Domain.Common;

/// <summary>
/// Marks an entity as belonging to a tenant. Implemented by every entity except <c>Tenant</c>
/// itself (ADR-0011 §1) and <c>SigningCertificate</c> (platform-level, not tenant data — see its
/// own comment). The infrastructure layer applies a global query filter to every type that
/// implements this interface (ADR-0004, T-203); the filter, not the caller, decides what a query
/// can see.
/// </summary>
public interface ITenantScoped
{
    Guid TenantId { get; init; }
}
