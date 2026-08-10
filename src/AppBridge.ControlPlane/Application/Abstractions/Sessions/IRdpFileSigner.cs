namespace AppBridge.ControlPlane.Application.Abstractions.Sessions;

/// <summary>
/// Abstraction for signing RDP files with rdpsign.exe (ADR-0009).
/// Signature is used to ensure RDP integrity and prevent tampering.
/// Windows-only per ADR-0009 until alternative signer is implemented.
/// </summary>
public interface IRdpFileSigner
{
    /// <summary>
    /// Signs an RDP file content and returns the signed binary.
    /// </summary>
    /// <param name="rdpContent">The RDP file content to sign.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Signed RDP file binary (can be written directly to .rdp file).</returns>
    /// <exception cref="InvalidOperationException">If signing fails or certificate is unavailable.</exception>
    Task<byte[]> SignRdpFileAsync(string rdpContent, CancellationToken cancellationToken = default);
}
