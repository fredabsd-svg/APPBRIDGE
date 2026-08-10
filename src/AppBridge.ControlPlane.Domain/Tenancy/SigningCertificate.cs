using AppBridge.ControlPlane.Domain.Common;

namespace AppBridge.ControlPlane.Domain.Tenancy;

/// <summary>
/// MODELO-DE-DADOS.md §3.4. Metadata only — the private key is non-exportable in the machine's
/// certificate store (ADR-0009), never in this database. Deliberately NOT <see cref="ITenantScoped"/>:
/// the RDP signing certificate belongs to the Control Plane instance, not to a tenant, so it does
/// not carry a TenantId and is exempt from the global tenant filter (T-203).
/// </summary>
public sealed class SigningCertificate : AuditedEntity
{
    /// <summary>Distributed to workstations via GPO as a trusted publisher (ADR-0009).</summary>
    public required string Thumbprint { get; set; }

    public required string Subject { get; set; }

    public required string Issuer { get; set; }

    public DateTimeOffset ValidFrom { get; set; }

    public DateTimeOffset ValidTo { get; set; }

    public CertificateStatus Status { get; set; } = CertificateStatus.Active;

    public DateTimeOffset? ActivatedAt { get; set; }

    public DateTimeOffset? RetiredAt { get; set; }
}
