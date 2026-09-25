namespace AppBridge.ControlPlane.Launching;

public interface IRdpFileSigner
{
    Task<byte[]> SignAsync(byte[] descriptor, string certificateThumbprint, CancellationToken cancellationToken);
}

public sealed class RdpSigningOptions
{
    public string? RdpsignPath { get; set; }
    public int TimeoutSeconds { get; set; } = 15;
}
