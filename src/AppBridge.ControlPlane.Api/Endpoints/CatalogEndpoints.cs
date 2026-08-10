using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
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
/// T-403 adds <c>ETag</c>/<c>If-None-Match</c> (RF-015): the tag is a content hash of the response
/// actually being sent for this user, not a version counter tracked separately from it — the
/// catalog changes for a user for two independent reasons (an <c>Application</c> row changes, or
/// their <c>ApplicationPermission</c> set does), and hashing the materialized result is the one
/// place both already show up, instead of trying to track "what changed" in two places that could
/// drift out of sync with each other.
///
/// The icon endpoint (PD-03) is T-404's — not built here.
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
        var etag = ComputeETag(items);
        httpContext.Response.Headers.ETag = etag;

        if (RequestHasMatchingETag(httpContext.Request, etag))
        {
            // RF-015: nothing beyond the status line and the two headers already set — the
            // periodic sync this exists for must cost close to nothing when the catalog hasn't
            // moved.
            return Results.StatusCode(StatusCodes.Status304NotModified);
        }

        return Results.Ok(new CatalogResponse(items, NextCursor: null));
    }

    private static bool RequestHasMatchingETag(HttpRequest request, string etag)
    {
        var ifNoneMatch = request.Headers.IfNoneMatch;
        return ifNoneMatch.Count > 0
            && ifNoneMatch.SelectMany(value => (value ?? string.Empty).Split(','))
                .Select(value => value.Trim())
                .Any(value => value == "*" || value == etag);
    }

    /// <summary>
    /// A strong validator over exactly what the client would otherwise receive — two responses
    /// hash the same if and only if they'd render identically, so there's no separate "did
    /// anything change" tracking to keep honest against the actual response shape.
    /// </summary>
    private static string ComputeETag(IReadOnlyList<CatalogApplication> items)
    {
        var canonical = string.Join(
            '|', items.Select(item => $"{item.Id}:{item.DisplayName}:{item.Description}:{item.LaunchMode}:{item.Available}"));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return $"\"cat-{Convert.ToHexString(hash)[..16].ToLowerInvariant()}\"";
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
