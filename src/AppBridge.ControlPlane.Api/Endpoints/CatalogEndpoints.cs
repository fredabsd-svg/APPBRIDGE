using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AppBridge.ControlPlane.Domain.Catalog;
using AppBridge.ControlPlane.Infrastructure;
using AppBridge.ControlPlane.Infrastructure.Authorization;
using Microsoft.EntityFrameworkCore;

namespace AppBridge.ControlPlane.Api.Endpoints;

/// <summary>
/// T-402: <c>GET /v1/applications</c> (API.md §3, RF-011, RF-013). The first
/// <c>[Authorize]</c>-gated route in the Control Plane — reachable only with a valid access token,
/// resolved to a tenant by <c>TenantResolutionMiddleware</c> before this handler ever runs.
///
/// <c>ETag</c>/<c>If-None-Match</c> (RF-015) is T-403's job, and the icon endpoint (PD-03) is
/// T-404's — neither is built here.
/// </summary>
public static class CatalogEndpoints
{
    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/v1/applications", GetApplications).RequireAuthorization();
        return app;
    }

    private static async Task<IResult> GetApplications(
        HttpContext httpContext,
        AppBridgeDbContext dbContext,
        IAuthorizationService authorizationService,
        CancellationToken cancellationToken)
    {
        var userAccountId = Guid.Parse(httpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

        // RF-011: only currently-authorized applications ever reach the response — not filtered
        // out afterward, selected in from the start, so there's no code path where an unauthorized
        // application is briefly materialized and then has to be remembered to be removed.
        var authorizedApplicationIds = await authorizationService.GetAuthorizedApplicationIdsAsync(userAccountId, cancellationToken);

        var applications = await dbContext.Applications
            .Where(a => a.Status == ApplicationStatus.Published && authorizedApplicationIds.Contains(a.Id))
            .OrderBy(a => a.DisplayName)
            .ToListAsync(cancellationToken);

        var items = applications.Select(ToCatalogApplication).ToList();
        return Results.Ok(new CatalogResponse(items, NextCursor: null));
    }

    private static CatalogApplication ToCatalogApplication(Application application) => new(
        Id: application.Id,
        DisplayName: application.DisplayName,
        Description: application.Description,
        IconUrl: $"/v1/applications/{application.Id}/icon",
        LaunchMode: application.LaunchMode == LaunchMode.RemoteApp ? "remote_app" : "confined_desktop",
        ProtocolUri: $"appbridge://launch/{application.Id}",
        // No host-health signal exists yet (GetHostHealthAsync is V2, ARQUITETURA.md §4.2) — every
        // application a user is authorized for and that's Published is reported available; nothing
        // in this codebase can currently distinguish "temporarily under maintenance" from that.
        Available: true);
}

public sealed record CatalogResponse(IReadOnlyList<CatalogApplication> Items, string? NextCursor);

public sealed record CatalogApplication(
    Guid Id,
    string DisplayName,
    string? Description,
    string IconUrl,
    string LaunchMode,
    string ProtocolUri,
    bool Available);
