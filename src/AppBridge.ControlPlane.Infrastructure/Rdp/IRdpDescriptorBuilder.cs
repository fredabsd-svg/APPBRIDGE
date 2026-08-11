namespace AppBridge.ControlPlane.Infrastructure.Rdp;

/// <summary>
/// T-501 (RF-018, ARQUITETURA.md §4 <c>RdpDescriptorBuilder</c>): builds the **unsigned** <c>.rdp</c>
/// file content for a RemoteApp launch. Pure and stateless — no host resolution (that's
/// <c>ISessionBackend</c>, T-503, not built yet) and no signing (that's <c>IRdpFileSigner</c>,
/// T-502, not built yet). What this returns is exactly what T-502 will hand to <c>rdpsign.exe</c>,
/// unmodified — a client only ever sees the signed result.
/// </summary>
public interface IRdpDescriptorBuilder
{
    string Build(RdpConnectionParameters parameters);
}

/// <summary>
/// The minimal connection info the descriptor needs. Host resolution (which session host, load,
/// affinity) is <c>ISessionBackend</c>'s job — this record is what that future component would hand
/// to <see cref="IRdpDescriptorBuilder"/>, not something T-501 resolves itself.
/// </summary>
public sealed record RdpConnectionParameters(string HostAddress, string RemoteAppAlias, string RemoteAppDisplayName);
