using AppBridge.ControlPlane.Domain.Entities;

namespace AppBridge.ControlPlane.Launching;

public sealed record SessionBackendTarget(SessionHost Host, bool SessionReused, Guid? SessionId);

/// <summary>Sessão como o backend a vê; o usuário é identificado pelo SID, nunca pelo nome (ADR-0022).</summary>
public sealed record BackendSessionSnapshot(
    string HostFqdn,
    string BackendSessionId,
    string? UserSid,
    DateTimeOffset? CreatedAt);

public interface ISessionBackend
{
    Task<SessionBackendTarget?> ResolveHostAsync(
        RemoteApplication application,
        UserAccount user,
        CancellationToken cancellationToken);

    Task CancelSessionAsync(Guid sessionId, string reason, CancellationToken cancellationToken);

    /// <summary>Sessões ativas ou desconectadas nos hosts informados; falha quando o backend não responde.</summary>
    Task<IReadOnlyList<BackendSessionSnapshot>> ListActiveSessionsAsync(
        IReadOnlyCollection<SessionHost> hosts,
        CancellationToken cancellationToken);
}

public sealed class RdsSessionOptions
{
    public string? PowerShellPath { get; set; }
    public int CommandTimeoutSeconds { get; set; } = 15;

    /// <summary>FQDN do Connection Broker consultado pela reconciliação (ADR-0022).</summary>
    public string? ConnectionBroker { get; set; }
}
