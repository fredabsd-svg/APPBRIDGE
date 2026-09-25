using System.Diagnostics;
using System.ComponentModel;
using System.Text;
using Microsoft.Extensions.Options;

namespace AppBridge.ControlPlane.Launching;

public sealed class RdpSignExeSigner(
    IOptions<RdpSigningOptions> options,
    ILogger<RdpSignExeSigner> logger) : IRdpFileSigner
{
    public async Task<byte[]> SignAsync(byte[] descriptor, string certificateThumbprint, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new RdpSigningException("A assinatura RDP exige Windows.");
        }

        var thumbprint = string.Concat(certificateThumbprint.Where(character => !char.IsWhiteSpace(character)));
        if (thumbprint.Length != 64 || !thumbprint.All(Uri.IsHexDigit))
        {
            throw new RdpSigningException("A impressão digital SHA-256 do certificado é inválida.");
        }

        var tempDirectory = Path.Combine(Path.GetTempPath(), "AppBridge", "rdp-signing");
        Directory.CreateDirectory(tempDirectory);
        var descriptorPath = Path.Combine(tempDirectory, $"{Guid.CreateVersion7():N}.rdp");
        try
        {
            await File.WriteAllBytesAsync(descriptorPath, descriptor, cancellationToken);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(options.Value.TimeoutSeconds, 1, 60)));

            var startInfo = new ProcessStartInfo
            {
                FileName = ResolveExecutablePath(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("/sha256");
            startInfo.ArgumentList.Add(thumbprint);
            startInfo.ArgumentList.Add("/q");
            startInfo.ArgumentList.Add(Path.GetFullPath(descriptorPath));

            using var process = Process.Start(startInfo)
                ?? throw new RdpSigningException("Não foi possível iniciar rdpsign.exe.");
            var stdoutTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(timeout.Token);
            try
            {
                await process.WaitForExitAsync(timeout.Token);
                await Task.WhenAll(stdoutTask, stderrTask);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                    // O processo já encerrou.
                }

                throw new RdpSigningException("A assinatura do arquivo RDP expirou.");
            }

            if (process.ExitCode != 0)
            {
                logger.LogError("rdpsign.exe terminou com código {ExitCode}.", process.ExitCode);
                throw new RdpSigningException("O arquivo RDP não pôde ser assinado.");
            }

            var signedDescriptor = await File.ReadAllBytesAsync(descriptorPath, cancellationToken);
            if (!Encoding.UTF8.GetString(signedDescriptor).Contains("signature:s:", StringComparison.OrdinalIgnoreCase))
            {
                throw new RdpSigningException("rdpsign.exe não gerou uma assinatura no descritor.");
            }

            return signedDescriptor;
        }
        catch (RdpSigningException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or Win32Exception)
        {
            logger.LogError(exception, "Falha ao assinar arquivo RDP.");
            throw new RdpSigningException("O arquivo RDP não pôde ser assinado.", exception);
        }
        finally
        {
            try
            {
                File.Delete(descriptorPath);
            }
            catch (IOException exception)
            {
                logger.LogWarning(exception, "Não foi possível remover um arquivo RDP temporário.");
            }
            catch (UnauthorizedAccessException exception)
            {
                logger.LogWarning(exception, "Não foi possível remover um arquivo RDP temporário.");
            }
        }
    }

    private string ResolveExecutablePath()
        => options.Value.RdpsignPath
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "rdpsign.exe");
}

public sealed class RdpSigningException(string message, Exception? innerException = null)
    : Exception(message, innerException);
