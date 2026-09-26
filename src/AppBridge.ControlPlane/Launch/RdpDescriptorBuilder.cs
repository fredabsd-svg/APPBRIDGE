using System.Text;
using System.Text.RegularExpressions;
using AppBridge.ControlPlane.Domain.Entities;

namespace AppBridge.ControlPlane.Launching;

public sealed class RdpDescriptorBuilder
{
    private static readonly Regex HostNamePattern = new(
        "^(?=.{1,253}\\z)(?:[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?)(?:\\.(?:[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?))*\\z",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex UserPrincipalNamePattern = new(
        "^[A-Za-z0-9._%+-]{1,64}@[A-Za-z0-9.-]{1,189}\\z",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public byte[] Build(
        RemoteApplication application,
        SessionHost host,
        UserAccount user,
        RedirectionPolicy policy)
    {
        if (!HostNamePattern.IsMatch(host.Fqdn))
        {
            throw new RdpDescriptorException("O FQDN do host contém caracteres inválidos.");
        }

        if (!UserPrincipalNamePattern.IsMatch(user.Upn))
        {
            throw new RdpDescriptorException("O UPN do usuário contém caracteres inválidos.");
        }

        if (!Regex.IsMatch(application.RemoteAppAlias, "^[A-Za-z0-9_.-]{1,128}\\z", RegexOptions.CultureInvariant))
        {
            throw new RdpDescriptorException("O alias do aplicativo contém caracteres inválidos.");
        }

        var lines = new List<string>
        {
            $"full address:s:{host.Fqdn}:3389",
            $"username:s:{user.Upn}",
            "prompt for credentials:i:0",
            "authentication level:i:2",
            "enablecredsspsupport:i:1",
            $"remoteapplicationmode:i:{(application.LaunchMode == Domain.Enums.ApplicationLaunchMode.RemoteApp ? 1 : 0)}",
            $"remoteapplicationprogram:s:||{application.RemoteAppAlias}",
            $"alternate shell:s:||{application.RemoteAppAlias}",
            $"redirectprinters:i:{ToInt(policy.AllowPrinter)}",
            $"redirectsmartcards:i:{ToInt(policy.AllowSmartcard)}",
            $"redirectclipboard:i:{ToInt(policy.AllowClipboard)}",
            $"drivestoredirect:s:{(policy.AllowDrives ? "*" : string.Empty)}",
            $"redirectcomports:i:{ToInt(policy.AllowSerialPorts)}",
            $"audiomode:i:{(policy.AllowAudioOut ? 0 : 2)}",
            $"audiocapturemode:i:{ToInt(policy.AllowAudioIn)}",
            $"usbdevicestoredirect:s:{(policy.AllowOtherUsb ? "*" : string.Empty)}"
        };

        return Encoding.UTF8.GetBytes(string.Join("\r\n", lines) + "\r\n");
    }

    private static int ToInt(bool value) => value ? 1 : 0;

}

public sealed class RdpDescriptorException(string message) : Exception(message);
