using AppBridge.ControlPlane.Application.Abstractions.Authentication;
using AppBridge.ControlPlane.Application.Dtos.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace AppBridge.ControlPlane.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthenticationService _authenticationService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthenticationService authenticationService, ILogger<AuthController> logger)
    {
        _authenticationService = authenticationService;
        _logger = logger;
    }

    /// <summary>
    /// Authenticate user and get JWT token
    /// </summary>
    /// <remarks>
    /// Returns a JWT bearer token that should be included in the Authorization header for subsequent requests.
    /// Token format: Authorization: Bearer {token}
    ///
    /// For MVP-0, password authentication is placeholder. Production will use Azure AD/Entra ID (ADR-0001).
    /// </remarks>
    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AuthTokenDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TenantId) || string.IsNullOrWhiteSpace(request.UserIdentifier) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "TenantId, UserIdentifier, and Password are required" });
        }

        if (!Guid.TryParse(request.TenantId, out var tenantId))
        {
            return BadRequest(new { message = "Invalid TenantId format" });
        }

        try
        {
            var token = await _authenticationService.AuthenticateAsync(
                request.UserIdentifier,
                request.Password,
                tenantId,
                cancellationToken);

            _logger.LogInformation("User {UserIdentifier} logged in successfully", request.UserIdentifier);
            return Ok(token);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { message = "Invalid credentials" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication error for user {UserIdentifier}", request.UserIdentifier);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred during authentication" });
        }
    }
}

public class LoginRequestDto
{
    public string TenantId { get; set; } = null!;
    public string UserIdentifier { get; set; } = null!;
    public string Password { get; set; } = null!;
}
