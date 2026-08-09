namespace AppBridge.ControlPlane.Core.Configuration;

public class AuthenticationOptions
{
    public AzureAdOptions AzureAd { get; set; } = new();
    public BearerSchemeOptions Schemes { get; set; } = new();
}

public class AzureAdOptions
{
    public string TenantId { get; set; } = null!;
    public string ClientId { get; set; } = null!;
    public string ClientSecret { get; set; } = null!;
    public string Instance { get; set; } = "https://login.microsoftonline.com/";
    public string? Authority { get; set; }
    public bool ValidateAuthority { get; set; } = true;
}

public class BearerSchemeOptions
{
    public bool ValidateAudience { get; set; } = false;
    public bool ValidateIssuer { get; set; } = true;
    public bool ValidateLifetime { get; set; } = true;
    public TimeSpan ClockSkew { get; set; } = TimeSpan.FromSeconds(5);
}
