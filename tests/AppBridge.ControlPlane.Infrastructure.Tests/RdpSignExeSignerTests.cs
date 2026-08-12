using System.Diagnostics;
using System.Runtime.CompilerServices;
using AppBridge.ControlPlane.Infrastructure.Rdp;
using Xunit;

namespace AppBridge.ControlPlane.Infrastructure.Tests;

/// <summary>
/// T-502 (ADR-0009). <c>rdpsign.exe</c> is a real Windows tool this environment (Linux sandbox)
/// does not have, so these tests cannot verify that <see cref="RdpSignExeSigner"/> produces a
/// signature a Windows client would accept — nobody can claim that from here. What they verify,
/// honestly, is the process orchestration around any external signer: the right arguments reach
/// it, the signed file is read back, a non-zero exit or a hung process become
/// <see cref="RdpSigningFailedException"/> rather than a silently-degraded result, and the temp
/// file never survives the call either way. The fixture scripts under <c>fixtures/</c> stand in for
/// <c>rdpsign.exe</c>'s CLI contract (<c>/sha256 &lt;thumbprint&gt; &lt;file&gt;</c>) without being
/// it.
/// </summary>
public sealed class RdpSignExeSignerTests
{
    private const string Thumbprint = "AA11BB22CC33DD44EE55FF66AA77BB88CC99DD00";

    private static string FixturePath(string fileName, [CallerFilePath] string sourceFilePath = "") =>
        Path.Combine(Path.GetDirectoryName(sourceFilePath)!, "fixtures", fileName);

    private static RdpSignExeSigner NewSigner(string fixtureScript, TimeSpan? timeout = null) => new(new RdpSignerOptions
    {
        CertificateThumbprint = Thumbprint,
        ExecutablePath = FixturePath(fixtureScript),
        Timeout = timeout ?? TimeSpan.FromSeconds(5),
    });

    private static int CountLeftoverTempFiles() => Directory.GetFiles(Path.GetTempPath(), "appbridge-rdp-*.rdp").Length;

    [Fact]
    public async Task Successful_signing_passes_the_thumbprint_and_returns_the_file_content_read_back()
    {
        var before = CountLeftoverTempFiles();
        var signer = NewSigner("fake-rdpsign-success.sh");

        var signed = await signer.SignAsync("full address:s:host\r\n");

        Assert.Contains("full address:s:host", signed); // the original content is still there
        Assert.Contains($"signed-with:{Thumbprint}", signed); // proves the right argument reached the process
        Assert.Equal(before, CountLeftoverTempFiles()); // the temp file didn't survive the call
    }

    [Fact]
    public async Task A_nonzero_exit_code_becomes_RdpSigningFailedException_with_stderr_in_the_message()
    {
        var before = CountLeftoverTempFiles();
        var signer = NewSigner("fake-rdpsign-fail.sh");

        var ex = await Assert.ThrowsAsync<RdpSigningFailedException>(() => signer.SignAsync("full address:s:host\r\n"));

        Assert.Contains("certificate not found", ex.Message);
        Assert.Equal(before, CountLeftoverTempFiles());
    }

    [Fact]
    public async Task A_hung_process_is_killed_and_reported_as_a_timeout_instead_of_hanging_the_caller()
    {
        var before = CountLeftoverTempFiles();
        var signer = NewSigner("fake-rdpsign-hang.sh", timeout: TimeSpan.FromMilliseconds(300));
        var stopwatch = Stopwatch.StartNew();

        var ex = await Assert.ThrowsAsync<RdpSigningFailedException>(() => signer.SignAsync("full address:s:host\r\n"));

        // The fixture sleeps 30s; if this weren't enforcing its own timeout, the assertion below
        // would time out the whole test run rather than merely fail it.
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Took {stopwatch.Elapsed} — timeout wasn't enforced.");
        Assert.Contains("did not finish within", ex.Message);
        Assert.Equal(before, CountLeftoverTempFiles());
    }

    [Fact]
    public async Task An_executable_that_does_not_exist_becomes_RdpSigningFailedException()
    {
        var before = CountLeftoverTempFiles();
        var signer = new RdpSignExeSigner(new RdpSignerOptions
        {
            CertificateThumbprint = Thumbprint,
            ExecutablePath = "/definitely/not/a/real/rdpsign.exe",
        });

        await Assert.ThrowsAsync<RdpSigningFailedException>(() => signer.SignAsync("full address:s:host\r\n"));

        Assert.Equal(before, CountLeftoverTempFiles());
    }
}
