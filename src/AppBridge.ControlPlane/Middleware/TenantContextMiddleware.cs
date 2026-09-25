using System.Security.Claims;
using AppBridge.ControlPlane.Data;

namespace AppBridge.ControlPlane.Middleware;

public sealed class TenantContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, TenantContext tenantContext)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var tenantClaim = context.User.FindFirstValue("tenant_id");
            if (!Guid.TryParse(tenantClaim, out var tenantId) || tenantId == Guid.Empty)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            tenantContext.Bind(tenantId);
        }

        await next(context);
    }
}
