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

        // ADR-0023: a troca usa o access token da API do AppBridge, não o ID token do launcher.
        return string.IsNullOrWhiteSpace(result.AccessToken)
            ? throw new InvalidOperationException("O Entra ID não retornou um token para a API do AppBridge.")
            : result.AccessToken;
    }
}
