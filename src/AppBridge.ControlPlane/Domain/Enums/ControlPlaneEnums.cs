namespace AppBridge.ControlPlane.Domain.Enums;

public enum TenantStatus
{
    Active,
    Suspended,
    Terminated
}

public enum RetentionCategory
{
    Access,
    Administrative,
    CertificateUsage
}

public enum SigningCertificateStatus
{
    Active,
    Superseded,
    Revoked
}

public enum UserAccountStatus
{
    Active,
    Disabled
}

public enum GroupSource
{
    Directory,
    Local
}

public enum ApplicationLaunchMode
{
    RemoteApp,
    ConfinedDesktop
}

public enum ApplicationStatus
{
    Draft,
    Published,
    Retired
}

public enum SessionBackendType
{
    Rds,
    Avd
}

public enum SessionHostStatus
{
    Online,
    Draining,
    Offline
}

public enum SessionEndReason
{
    Logoff,
    DisconnectTimeout,
    TerminatedByAdmin,
    Revoked,
    ReconciledMissing,
    StaleExpired
}

public enum LaunchPurpose
{
    UserInitiated,
    Prelaunch
}

public enum LaunchOutcome
{
    Granted,
    DeniedPermission,
    DeniedQuota,
    DeniedHostUnavailable,
    ErrorSigning,
    ErrorInternal
}

public enum AccessEventType
{
    Authentication,
    Logout,
    SessionStarted,
    SessionEnded
}

public enum AccessEventResult
{
    Success,
    Failure
}
