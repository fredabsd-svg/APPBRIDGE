namespace AppBridge.ControlPlane.Core.Configuration;

public class RdpSigningOptions
{
    public string CertificatePath { get; set; } = null!;
    public string CertificatePassword { get; set; } = null!;
    public int FileValiditySeconds { get; set; } = 60;
    public string SigningAlgorithm { get; set; } = "SHA256";
}
