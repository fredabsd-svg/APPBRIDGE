using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppBridge.ControlPlane.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly ILogger<HealthController> _logger;

    public HealthController(ILogger<HealthController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Health check endpoint (public)
    /// </summary>
    [HttpGet("status")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Status()
    {
        return Ok(new { status = "healthy", timestamp = DateTimeOffset.UtcNow });
    }

    /// <summary>
    /// Protected health check endpoint (requires authentication)
    /// </summary>
    [HttpGet("protected")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Protected()
    {
        var userId = User.FindFirst("sub")?.Value;
        var tenantId = User.FindFirst("tenant_id")?.Value;

        _logger.LogInformation("Protected endpoint accessed by user {UserId} in tenant {TenantId}", userId, tenantId);

        return Ok(new
        {
            status = "authenticated",
            userId,
            tenantId,
            timestamp = DateTimeOffset.UtcNow,
        });
    }
}
