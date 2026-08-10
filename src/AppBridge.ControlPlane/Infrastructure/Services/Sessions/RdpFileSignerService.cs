using AppBridge.ControlPlane.Application.Abstractions.Sessions;
using System.Text;

namespace AppBridge.ControlPlane.Infrastructure.Services.Sessions;

/// <summary>
/// RDP file signer using rdpsign.exe (Windows only per ADR-0009).
/// This stub implementation is used for testing. In production on Windows,
/// this would invoke rdpsign.exe via ProcessStartInfo.
/// </summary>
public class RdpFileSignerService : IRdpFileSigner
{
    private readonly ILogger<RdpFileSignerService> _logger;

    public RdpFileSignerService(ILogger<RdpFileSignerService> logger)
    {
        _logger = logger;
    }

    public Task<byte[]> SignRdpFileAsync(string rdpContent, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Signing RDP file (stub implementation)");

            var contentBytes = Encoding.UTF8.GetBytes(rdpContent);
            var signatureBytes = Encoding.UTF8.GetBytes("SIGNED:");

            var result = new byte[signatureBytes.Length + contentBytes.Length];
            signatureBytes.CopyTo(result, 0);
            contentBytes.CopyTo(result, signatureBytes.Length);

            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sign RDP file");
            throw new InvalidOperationException("Failed to sign RDP file", ex);
        }
    }
}
