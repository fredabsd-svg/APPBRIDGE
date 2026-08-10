namespace AppBridge.ControlPlane.Domain.Tenancy;

/// <summary>The three trail families and their retention rules (ADR-0007).</summary>
public enum RetentionCategory
{
    Access,
    Administrative,
    CertificateUsage,
}
