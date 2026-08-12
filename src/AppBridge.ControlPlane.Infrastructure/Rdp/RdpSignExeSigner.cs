using System.Diagnostics;

namespace AppBridge.ControlPlane.Infrastructure.Rdp;

/// <summary>See <see cref="IRdpFileSigner"/>. ADR-0009 item 2: invokes <c>rdpsign.exe</c> in a separate process, with a timeout, and treats any failure as a launch failure — never returns an unsigned file.</summary>
public sealed class RdpSignExeSigner(RdpSignerOptions options) : IRdpFileSigner
{
    public async Task<string> SignAsync(string unsignedRdpContent, CancellationToken cancellationToken = default)
    {
        var tempFilePath = Path.Combine(Path.GetTempPath(), $"appbridge-rdp-{Guid.NewGuid()}.rdp");
        try
        {
            // rdpsign.exe signs a file in place — there is no stdin/stdout form of this tool
            // (ADR-0009: it isn't a public, implementable spec, it's this exact Microsoft binary).
            await File.WriteAllTextAsync(tempFilePath, unsignedRdpContent, cancellationToken);
            await RunSignerAsync(tempFilePath, cancellationToken);
            return await File.ReadAllTextAsync(tempFilePath, cancellationToken);
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
    }

    private async Task RunSignerAsync(string rdpFilePath, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = options.ExecutablePath,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("/sha256");
        startInfo.ArgumentList.Add(options.CertificateThumbprint);
        startInfo.ArgumentList.Add(rdpFilePath);

        using var process = new Process { StartInfo = startInfo };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            throw new RdpSigningFailedException($"Failed to start '{options.ExecutablePath}'.", ex);
        }

        using var timeoutCts = new CancellationTokenSource(options.Timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        string stderr;
        try
        {
            var stderrTask = process.StandardError.ReadToEndAsync(linkedCts.Token);
            var stdoutTask = process.StandardOutput.ReadToEndAsync(linkedCts.Token);
            await process.WaitForExitAsync(linkedCts.Token);
            stderr = await stderrTask;
            await stdoutTask; // drained so a chatty process can't deadlock on a full pipe buffer; content unused
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new RdpSigningFailedException($"'{options.ExecutablePath}' did not finish within {options.Timeout}.");
        }
        finally
        {
            if (!process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Best effort — the process is being abandoned either way; a failed kill
                    // doesn't change the outcome already being reported to the caller.
                }
            }
        }

        if (process.ExitCode != 0)
        {
            throw new RdpSigningFailedException($"'{options.ExecutablePath}' exited with code {process.ExitCode}: {stderr}");
        }
    }
}

/// <summary>
/// <see cref="CertificateThumbprint"/> comes from <c>APPBRIDGE_RDP_SIGNING_THUMBPRINT</c>
/// (docs/SETUP-DEV.md §3) — not a secret (MODELO-DE-DADOS.md §3.4: the private key is what's
/// sensitive, and it never leaves the machine's certificate store, ADR-0009 item 3), just an
/// identifier. Read once at startup, the same pattern as <c>JwtSigningOptions</c>.
/// </summary>
public sealed class RdpSignerOptions
{
    public required string CertificateThumbprint { get; init; }

    /// <summary>Defaults to relying on <c>PATH</c> resolution, the common case for a real Windows install.</summary>
    public string ExecutablePath { get; init; } = "rdpsign.exe";

    /// <summary><c>PREMISSA:</c> not measured — RNF-029 is the requirement this should eventually be tuned against, once real timing exists to tune it with.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);
}
