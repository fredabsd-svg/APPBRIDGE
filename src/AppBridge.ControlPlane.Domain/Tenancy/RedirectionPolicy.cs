using AppBridge.ControlPlane.Domain.Common;

namespace AppBridge.ControlPlane.Domain.Tenancy;

/// <summary>
/// MODELO-DE-DADOS.md §3.3. Baseline policy per tenant (<see cref="ApplicationId"/> null), with
/// optional per-application overrides. Defaults match ADR-0008: printer, smart card, clipboard and
/// audio-out allowed; local drives, serial ports, audio-in and other USB denied.
/// </summary>
public sealed class RedirectionPolicy : TenantScopedEntity
{
    /// <summary>Null means this row is the tenant's baseline, not an app-specific override.</summary>
    public Guid? ApplicationId { get; set; }

    public bool AllowPrinter { get; set; } = true;

    public bool AllowSmartcard { get; set; } = true;

    public bool AllowClipboard { get; set; } = true;

    public bool AllowAudioOut { get; set; } = true;

    public bool AllowDrives { get; set; }

    public bool AllowSerialPorts { get; set; }

    public bool AllowAudioIn { get; set; }

    public bool AllowOtherUsb { get; set; }

    /// <summary>
    /// Required on every application-specific override row (ADR-0008 condition 2). A CHECK
    /// constraint enforces "override rows carry a reason" — it cannot, by itself, verify that the
    /// values actually differ from the tenant baseline, since that would mean comparing against a
    /// different row. Requiring a reason on every override is the enforceable proxy for that.
    /// </summary>
    public string? ExceptionReason { get; set; }
}
