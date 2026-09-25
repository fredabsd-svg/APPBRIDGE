using Microsoft.Identity.Client;

namespace AppBridge.Launcher;

internal sealed class EntraIdentityTokenProvider(LauncherSettings settings)
{
    public async Task<string> AcquireIdentityTokenAsync(CancellationToken cancellationToken)
    {
        var application = PublicClientApplicationBuilder
            .Create(settings.EntraClientId)
            .WithAuthority(settings.EntraAuthority)
            .WithDefaultRedirectUri()
            .Build();

        var result = await application
            .AcquireTokenInteractive([settings.EntraScope])
            .WithPrompt(Prompt.SelectAccount)
            .ExecuteAsync(cancellationToken);

        return string.IsNullOrWhiteSpace(result.IdToken)
            ? throw new InvalidOperationException("O Entra ID não retornou um token de identidade.")
            : result.IdToken;
    }
}
