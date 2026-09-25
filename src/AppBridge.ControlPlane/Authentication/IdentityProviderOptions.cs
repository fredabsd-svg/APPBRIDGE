namespace AppBridge.ControlPlane.Authentication;

public sealed class IdentityProviderOptions
{
    public string? Authority { get; init; }
    public string? Audience { get; init; }
    public Dictionary<string, string> TenantMappings { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class ControlPlaneTokenOptions
{
    public string Issuer { get; set; } = "https://appbridge.local";
    public string Audience { get; set; } = "appbridge-launcher";
    public string SigningKey { get; set; } = string.Empty;
    public int LifetimeMinutes { get; set; } = 30;
}
