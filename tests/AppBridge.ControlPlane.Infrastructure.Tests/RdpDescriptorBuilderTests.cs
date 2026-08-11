using AppBridge.ControlPlane.Infrastructure.Rdp;
using Xunit;

namespace AppBridge.ControlPlane.Infrastructure.Tests;

/// <summary>
/// T-501 acceptance criterion: "`.rdp` gerado nega unidades locais e permite impressora
/// (ADR-0008)". Pure logic, no PostgreSQL — <see cref="RdpDescriptorBuilder"/> has no I/O.
/// </summary>
public sealed class RdpDescriptorBuilderTests
{
    private static readonly RdpConnectionParameters Parameters = new(
        HostAddress: "ab-rds01.escritorio-a.local",
        RemoteAppAlias: "dominio-contabil",
        RemoteAppDisplayName: "Domínio Contábil");

    private static string[] BuildLines() => new RdpDescriptorBuilder().Build(Parameters).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

    [Fact]
    public void Denies_local_drive_redirection()
    {
        Assert.Contains("drivestoredirect:s:", BuildLines());
    }

    [Fact]
    public void Allows_printer_redirection()
    {
        Assert.Contains("redirectprinters:i:1", BuildLines());
    }

    [Fact]
    public void Allows_smart_card_and_token_redirection()
    {
        Assert.Contains("redirectsmartcards:i:1", BuildLines());
    }

    [Fact]
    public void Allows_bidirectional_clipboard_redirection()
    {
        Assert.Contains("redirectclipboard:i:1", BuildLines());
    }

    [Fact]
    public void Denies_COM_port_redirection()
    {
        Assert.Contains("redirectcomports:i:0", BuildLines());
    }

    [Fact]
    public void Allows_audio_output_but_denies_audio_capture()
    {
        var lines = BuildLines();
        Assert.Contains("audiomode:i:0", lines);
        Assert.Contains("audiocapturemode:i:0", lines);
    }

    [Fact]
    public void Denies_generic_plug_and_play_device_redirection()
    {
        Assert.Contains("devicestoredirect:s:", BuildLines());
    }

    [Fact]
    public void Launches_in_RemoteApp_mode_with_the_given_alias_and_display_name()
    {
        var lines = BuildLines();
        Assert.Contains("remoteapplicationmode:i:1", lines);
        Assert.Contains("remoteapplicationprogram:s:||dominio-contabil", lines);
        Assert.Contains("remoteapplicationname:s:Domínio Contábil", lines);
    }

    [Fact]
    public void Points_at_the_given_host_address()
    {
        Assert.Contains("full address:s:ab-rds01.escritorio-a.local", BuildLines());
    }

    [Fact]
    public void Uses_CRLF_line_endings_regardless_of_the_building_host_OS()
    {
        var descriptor = new RdpDescriptorBuilder().Build(Parameters);
        Assert.DoesNotContain("\n", descriptor.Replace("\r\n", string.Empty));
    }
}
