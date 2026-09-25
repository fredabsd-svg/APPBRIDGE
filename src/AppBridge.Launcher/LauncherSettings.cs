namespace AppBridge.Launcher;

internal sealed record LauncherSettings(Uri ApiBaseAddress, string EntraClientId, Uri EntraAuthority, string EntraScope)
{
    public static LauncherSettings Load()
    {
        var apiAddressText = RequiredEnvironmentVariable("APPBRIDGE_API_BASE_URL");
        if (!Uri.TryCreate(apiAddressText, UriKind.Absolute, out var apiAddress)
            || (apiAddress.Scheme != Uri.UriSchemeHttps && !apiAddress.IsLoopback))
        {
            throw new LauncherConfigurationException(
                "APPBRIDGE_API_BASE_URL deve ser uma URL HTTPS (HTTP só é permitido em localhost).");
        }

        if (!apiAddress.AbsolutePath.EndsWith('/'))
        {
            apiAddress = new Uri(apiAddress.AbsoluteUri + "/", UriKind.Absolute);
        }

        var clientId = RequiredEnvironmentVariable("APPBRIDGE_ENTRA_CLIENT_ID");
        if (!Guid.TryParse(clientId, out _))
        {
            throw new LauncherConfigurationException("APPBRIDGE_ENTRA_CLIENT_ID deve ser o ID GUID do registro de aplicativo Entra.");
        }

        var entraScope = RequiredEnvironmentVariable("APPBRIDGE_ENTRA_SCOPE");
        if (entraScope.Any(char.IsWhiteSpace))
        {
            throw new LauncherConfigurationException("APPBRIDGE_ENTRA_SCOPE deve conter um único escopo delegado.");
        }

        var authorityText = Environment.GetEnvironmentVariable("APPBRIDGE_ENTRA_AUTHORITY")
            ?? "https://login.microsoftonline.com/organizations";
        if (!Uri.TryCreate(authorityText, UriKind.Absolute, out var authority)
            || authority.Scheme != Uri.UriSchemeHttps)
        {
            throw new LauncherConfigurationException("APPBRIDGE_ENTRA_AUTHORITY deve ser uma URL HTTPS.");
        }

        return new LauncherSettings(apiAddress, clientId, authority, entraScope);
    }

    private static string RequiredEnvironmentVariable(string name)
    {
        var value = Environment.GetEnvironmentVariable(name)?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new LauncherConfigurationException($"Configure a variável de ambiente {name} antes de iniciar o launcher.");
        }

        return value;
    }
}

internal sealed class LauncherConfigurationException(string message) : Exception(message);
