using AppBridge.ControlPlane.Application.Abstractions.Context;
using AppBridge.ControlPlane.Application.Abstractions.Sessions;
using AppBridge.ControlPlane.Application.Dtos.Sessions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppBridge.ControlPlane.Api.Controllers;

[ApiController]
[Route("api/sessions")]
[Authorize]
public class SessionsController : ControllerBase
{
    private readonly ISessionService _sessionService;
    private readonly ITenantContextService _tenantContextService;
    private readonly ILogger<SessionsController> _logger;

    public SessionsController(
        ISessionService sessionService,
        ITenantContextService tenantContextService,
        ILogger<SessionsController> logger)
    {
        _sessionService = sessionService;
        _tenantContextService = tenantContextService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<SessionDto>>> ListUserSessions(CancellationToken cancellationToken)
    {
        if (_tenantContextService.UserId is not Guid userId)
        {
            _logger.LogWarning("UserId not found in tenant context");
            return Unauthorized();
        }

        if (_tenantContextService.TenantId is not Guid tenantId)
        {
            _logger.LogWarning("TenantId not found in tenant context");
            return Unauthorized();
        }

        var sessions = await _sessionService.ListUserSessionsAsync(tenantId, userId, cancellationToken);
        return Ok(sessions);
    }

    [HttpGet("{sessionId}")]
    public async Task<ActionResult<SessionDto>> GetSession(Guid sessionId, CancellationToken cancellationToken)
    {
        if (_tenantContextService.TenantId is not Guid tenantId)
        {
            return Unauthorized();
        }

        var session = await _sessionService.GetSessionAsync(tenantId, sessionId, cancellationToken);

        if (session == null)
        {
            return NotFound();
        }

        return Ok(session);
    }

    [HttpPost]
    public async Task<ActionResult<LaunchSessionResponseDto>> LaunchSession([FromBody] CreateSessionDto dto, CancellationToken cancellationToken)
    {
        if (_tenantContextService.TenantId is not Guid tenantId)
        {
            return Unauthorized();
        }

        if (_tenantContextService.UserId is not Guid userId)
        {
            return Unauthorized();
        }

        if (dto.ApplicationId == Guid.Empty)
        {
            return BadRequest("ApplicationId is required.");
        }

        try
        {
            var response = await _sessionService.LaunchSessionAsync(tenantId, userId, dto.ApplicationId, cancellationToken);
            return CreatedAtAction(nameof(GetSession), new { sessionId = response.SessionId }, response);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not published"))
        {
            return BadRequest("Application is not published.");
        }
    }

    [HttpPut("{sessionId}")]
    public async Task<ActionResult<SessionDto>> UpdateSession(Guid sessionId, [FromBody] UpdateSessionDto dto, CancellationToken cancellationToken)
    {
        if (_tenantContextService.TenantId is not Guid tenantId)
        {
            return Unauthorized();
        }

        try
        {
            var session = await _sessionService.UpdateSessionAsync(tenantId, sessionId, dto, cancellationToken);
            return Ok(session);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpDelete("{sessionId}")]
    public async Task<IActionResult> TerminateSession(Guid sessionId, [FromQuery] string? reason = null, CancellationToken cancellationToken = default)
    {
        if (_tenantContextService.TenantId is not Guid tenantId)
        {
            return Unauthorized();
        }

        try
        {
            await _sessionService.TerminateSessionAsync(tenantId, sessionId, reason, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
