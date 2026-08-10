using AppBridge.ControlPlane.Application.Abstractions.Applications;
using AppBridge.ControlPlane.Application.Abstractions.Context;
using AppBridge.ControlPlane.Application.Dtos.Applications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AppBridge.ControlPlane.Api.Controllers;

[ApiController]
[Route("api/applications")]
[Authorize]
public class ApplicationsController : ControllerBase
{
    private readonly IApplicationService _applicationService;
    private readonly ITenantContextService _tenantContextService;
    private readonly ILogger<ApplicationsController> _logger;

    public ApplicationsController(
        IApplicationService applicationService,
        ITenantContextService tenantContextService,
        ILogger<ApplicationsController> logger)
    {
        _applicationService = applicationService;
        _tenantContextService = tenantContextService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ApplicationDto>>> ListApplications(CancellationToken cancellationToken)
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

        var applications = await _applicationService.ListUserApplicationsAsync(tenantId, userId, cancellationToken);

        return Ok(applications);
    }

    [HttpGet("{applicationId}")]
    public async Task<ActionResult<ApplicationDto>> GetApplication(Guid applicationId, CancellationToken cancellationToken)
    {
        if (_tenantContextService.TenantId is not Guid tenantId)
        {
            return Unauthorized();
        }

        var application = await _applicationService.GetApplicationAsync(tenantId, applicationId, cancellationToken);

        if (application == null)
        {
            return NotFound();
        }

        return Ok(application);
    }

    [HttpPost]
    public async Task<ActionResult<ApplicationDto>> CreateApplication([FromBody] CreateApplicationDto dto, CancellationToken cancellationToken)
    {
        if (_tenantContextService.TenantId is not Guid tenantId)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(dto.Identifier) || string.IsNullOrWhiteSpace(dto.DisplayName) || string.IsNullOrWhiteSpace(dto.RemoteAppName))
        {
            return BadRequest("Identifier, DisplayName, and RemoteAppName are required.");
        }

        var application = await _applicationService.CreateApplicationAsync(tenantId, dto, cancellationToken);

        return CreatedAtAction(nameof(GetApplication), new { applicationId = application.Id }, application);
    }

    [HttpPut("{applicationId}")]
    public async Task<ActionResult<ApplicationDto>> UpdateApplication(Guid applicationId, [FromBody] UpdateApplicationDto dto, CancellationToken cancellationToken)
    {
        if (_tenantContextService.TenantId is not Guid tenantId)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(dto.DisplayName))
        {
            return BadRequest("DisplayName is required.");
        }

        try
        {
            var application = await _applicationService.UpdateApplicationAsync(tenantId, applicationId, dto, cancellationToken);
            return Ok(application);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPatch("{applicationId}/publish")]
    public async Task<ActionResult<ApplicationDto>> PublishApplication(Guid applicationId, [FromBody] PublishApplicationDto dto, CancellationToken cancellationToken)
    {
        if (_tenantContextService.TenantId is not Guid tenantId)
        {
            return Unauthorized();
        }

        try
        {
            var application = await _applicationService.PublishApplicationAsync(tenantId, applicationId, dto.IsPublished, cancellationToken);
            return Ok(application);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpDelete("{applicationId}")]
    public async Task<IActionResult> DeleteApplication(Guid applicationId, CancellationToken cancellationToken)
    {
        if (_tenantContextService.TenantId is not Guid tenantId)
        {
            return Unauthorized();
        }

        try
        {
            await _applicationService.DeleteApplicationAsync(tenantId, applicationId, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
