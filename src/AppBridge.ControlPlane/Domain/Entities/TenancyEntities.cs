using AppBridge.ControlPlane.Domain.Enums;

namespace AppBridge.ControlPlane.Domain.Entities;

public sealed class Tenant : MutableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public TenantStatus Status { get; set; }
    public string AdDomain { get; set; } = string.Empty;
    public string AdOuDn { get; set; } = string.Empty;
}

public sealed class RetentionPolicy : TenantMutableEntity
{
    public RetentionCategory Category { get; set; }
    public int RetentionMonths { get; set; }
}

public sealed class RedirectionPolicy : TenantMutableEntity
{
    public Guid? ApplicationId { get; set; }
    public bool AllowPrinter { get; set; } = true;
    public bool AllowSmartcard { get; set; } = true;
    public bool AllowClipboard { get; set; } = true;
    public bool AllowAudioOut { get; set; } = true;
    public bool AllowDrives { get; set; }
    public bool AllowSerialPorts { get; set; }
    public bool AllowAudioIn { get; set; }
    public bool AllowOtherUsb { get; set; }
    public string? ExceptionReason { get; set; }
}

public sealed class SigningCertificate : TenantMutableEntity
{
    public string Thumbprint { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public DateTimeOffset ValidFrom { get; set; }
    public DateTimeOffset ValidTo { get; set; }
    public SigningCertificateStatus Status { get; set; }
    public DateTimeOffset ActivatedAt { get; set; }
    public DateTimeOffset? RetiredAt { get; set; }
}
