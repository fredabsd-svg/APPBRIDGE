namespace AppBridge.ControlPlane.Infrastructure.Rdp;

/// <summary>See <see cref="IRdpDescriptorBuilder"/>.</summary>
public sealed class RdpDescriptorBuilder : IRdpDescriptorBuilder
{
    public string Build(RdpConnectionParameters parameters)
    {
        string[] lines =
        [
            $"full address:s:{parameters.HostAddress}",

            // RemoteApp launch (RF-018): the user gets the application's own window, never the
            // server desktop — that's the whole premise of the product, not just this property.
            "remoteapplicationmode:i:1",
            $"remoteapplicationprogram:s:||{parameters.RemoteAppAlias}",
            $"remoteapplicationname:s:{parameters.RemoteAppDisplayName}",

            // ADR-0008's redirection policy, one line per row of that table — nothing here is a
            // property this class invented; each maps to a decision the ADR already made.
            "redirectprinters:i:1", // Impressora local: permitido
            "redirectsmartcards:i:1", // Token/smart card USB (A3): permitido
            "redirectclipboard:i:1", // Área de transferência: permitido, bidirecional
            "drivestoredirect:s:", // Unidades locais: negado (lista vazia de letras = nenhuma redirecionada)
            "redirectcomports:i:0", // Portas COM/LPT: negado
            "audiomode:i:0", // Áudio de saída: permitido (toca na estação)
            "audiocapturemode:i:0", // Áudio de entrada (microfone): negado
            "devicestoredirect:s:", // Demais Plug and Play (USB genérico): negado
        ];

        // CRLF, not Environment.NewLine: the .rdp format is a Windows text format read by mstsc on
        // a Windows client regardless of which OS built it (ADR-0009 — the Control Plane itself is
        // a Windows component, but this class has no business depending on the host OS to get the
        // line ending right).
        return string.Join("\r\n", lines) + "\r\n";
    }
}
