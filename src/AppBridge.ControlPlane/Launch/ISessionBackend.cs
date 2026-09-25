using AppBridge.ControlPlane.Domain.Entities;

namespace AppBridge.ControlPlane.Launching;

public sealed record SessionBackendTarget(SessionHost Host, bool SessionReused, Guid? SessionId);

public interface ISessionBackend
{
    Task<SessionBackendTarget?> ResolveHostAsync(
        RemoteApplication application,
        UserAccount user,
        CancellationToken cancellationToken);

    Task CancelSessionAsync(Guid sessionId, string reason, CancellationToken cancellationToken);
}

public sealed class RdsSessionOptions
{
    public string? PowerShellPath { get; set; }
    public int CommandTimeoutSeconds { get; set; } = 15;
}
