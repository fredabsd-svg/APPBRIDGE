namespace AppBridge.ControlPlane.Authentication;

public sealed class IdentityProviderOptions
{
    public string? Authority { get; init; }
    /// <summary>Identificador da API do AppBridge no Entra; não é o client ID do launcher (ADR-0023).</summary>
    public string? Audience { get; init; }

    /// <summary>Client ID do launcher, conferido no <c>azp</c>/<c>appid</c> do access token.</summary>
    public string? ClientApplicationId { get; init; }

    /// <summary>Escopo delegado que o access token precisa trazer em <c>scp</c>.</summary>
    public string RequiredScope { get; init; } = "access_as_user";

    /// <summary>Idade máxima do token trocado, em minutos (1 a 60).</summary>
    public int MaxTokenAgeMinutes { get; init; } = 10;
    public Dictionary<string, string> TenantMappings { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class ControlPlaneTokenOptions
{
    public string Issuer { get; set; } = "https://appbridge.local";
    public string Audience { get; set; } = "appbridge-launcher";
    public string SigningKey { get; set; } = string.Empty;
    public int LifetimeMinutes { get; set; } = 30;
}
