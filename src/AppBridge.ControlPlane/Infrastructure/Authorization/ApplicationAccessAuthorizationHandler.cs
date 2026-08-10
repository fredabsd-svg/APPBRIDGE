using AppBridge.ControlPlane.Application.Abstractions.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace AppBridge.ControlPlane.Infrastructure.Authorization;

public class ApplicationAccessAuthorizationHandler : AuthorizationHandler<ApplicationAccessRequirement>
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ILogger<ApplicationAccessAuthorizationHandler> _logger;

    public ApplicationAccessAuthorizationHandler(
        IAuthorizationService authorizationService,
        ILogger<ApplicationAccessAuthorizationHandler> logger)
    {
        _authorizationService = authorizationService;
        _logger = logger;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ApplicationAccessRequirement requirement)
    {
        var userIdClaim = context.User.FindFirst("sub");
        var tenantIdClaim = context.User.FindFirst("tenant_id");

        if (userIdClaim == null || tenantIdClaim == null)
        {
            _logger.LogWarning("Missing required claims for application access check");
            context.Fail();
            return;
        }

        if (!Guid.TryParse(userIdClaim.Value, out var userId))
        {
            _logger.LogWarning("Invalid userId claim format: {UserId}", userIdClaim.Value);
            context.Fail();
            return;
        }

        if (!Guid.TryParse(tenantIdClaim.Value, out var tenantId))
        {
            _logger.LogWarning("Invalid tenantId claim format: {TenantId}", tenantIdClaim.Value);
            context.Fail();
            return;
        }

        var applicationIdFromRoute = context.HttpContext.GetRouteValue("applicationId");
        if (applicationIdFromRoute == null)
        {
            _logger.LogWarning("Missing applicationId in route parameters");
            context.Fail();
            return;
        }

        if (!Guid.TryParse(applicationIdFromRoute.ToString(), out var applicationId))
        {
            _logger.LogWarning("Invalid applicationId format in route: {ApplicationId}", applicationIdFromRoute);
            context.Fail();
            return;
        }

        var hasAccess = await _authorizationService.UserHasApplicationAccessAsync(
            userId, tenantId, applicationId, context.HttpContext.RequestAborted);

        if (hasAccess)
        {
            context.Succeed(requirement);
        }
        else
        {
            _logger.LogWarning(
                "Authorization failed: user {UserId} denied access to application {ApplicationId}",
                userId, applicationId);
            context.Fail();
        }
    }
}

public class ApplicationAccessRequirement : IAuthorizationRequirement
{
}
