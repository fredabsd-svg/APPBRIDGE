using AppBridge.ControlPlane.Application.Abstractions.Context;
using System.Security.Claims;

namespace AppBridge.ControlPlane.Infrastructure.Middleware;

public class TenantContextMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantContextMiddleware> _logger;

    public TenantContextMiddleware(RequestDelegate next, ILogger<TenantContextMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContextService tenantContextService)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var tenantIdClaim = context.User.FindFirst("tenant_id");
            var userIdClaim = context.User.FindFirst("sub");

            if (tenantIdClaim != null && Guid.TryParse(tenantIdClaim.Value, out var tenantId) &&
                userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
            {
                tenantContextService.SetContext(tenantId, userId);
                _logger.LogDebug("Tenant context set: TenantId={TenantId}, UserId={UserId}", tenantId, userId);
            }
            else
            {
                _logger.LogWarning("Failed to extract tenant context from claims");
            }
        }

        await _next(context);
    }
}

public static class TenantContextMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantContext(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<TenantContextMiddleware>();
    }
}
