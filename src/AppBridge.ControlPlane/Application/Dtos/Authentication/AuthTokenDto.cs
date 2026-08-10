namespace AppBridge.ControlPlane.Application.Dtos.Authentication;

public class AuthTokenDto
{
    public string AccessToken { get; set; } = null!;
    public string TokenType { get; set; } = "Bearer";
    public int ExpiresIn { get; set; }
}
