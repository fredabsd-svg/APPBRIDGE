using AppBridge.ControlPlane.Infrastructure.Tenancy;

namespace AppBridge.ControlPlane.Api.Middleware;

/// <summary>
/// T-402: the first <c>[Authorize]</c>-gated route (<c>GET /v1/applications</c>) needs
/// <see cref="TenantContext.TenantId"/> populated from something other than a database lookup — the
/// bootstrap problem T-301's login handler solves by resolving <c>Tenant.AdDomain</c> doesn't apply
/// once a caller already holds a session token: the tenant is right there in the <c>tenant_id</c>
/// claim <c>JwtSessionTokenIssuer</c> put on it (ADR-0017 §1), so re-querying the database for it
/// on every request would be redundant, not more correct.
///
/// Runs after <c>UseAuthentication()</c> so <see cref="HttpContext.User"/> is populated by the time
/// this executes, and before <c>UseAuthorization()</c>/endpoint execution so every tenant-scoped
/// query downstream (ADR-0004) sees the right tenant. A request that never authenticated (the
/// public <c>/v1/auth/*</c> endpoints, an anonymous request rejected before this point) leaves
/// <see cref="TenantContext.TenantId"/> null — nothing here needs to special-case "no token": an
/// unauthenticated request to an <c>[Authorize]</c> route is already rejected by
/// <c>UseAuthorization()</c> before any tenant-scoped query would run.
/// </summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, TenantContext tenantContext)
    {
        if (context.User.Identity?.IsAuthenticated == true
            && Guid.TryParse(context.User.FindFirst("tenant_id")?.Value, out var tenantId))
        {
            tenantContext.TenantId = tenantId;
        }

        await next(context);
    }
}
