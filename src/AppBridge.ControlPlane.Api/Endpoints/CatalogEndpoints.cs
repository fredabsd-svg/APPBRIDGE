using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AppBridge.ControlPlane.Api.Middleware;
using AppBridge.ControlPlane.Domain.Catalog;
using AppBridge.ControlPlane.Infrastructure;
using AppBridge.ControlPlane.Infrastructure.Authorization;
using AppBridge.ControlPlane.Infrastructure.Catalog;
using Microsoft.AspNetCore.Mvc;
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
/// T-404 adds <c>GET /v1/applications/{id}/icon</c> (PD-03): serves the PNG bytes
/// <see cref="IIconStorage"/> resolves from <c>Application.IconRef</c>, with the same
/// content-hash-as-<c>ETag</c> approach and a long <c>Cache-Control</c> — the icon changes rarely
/// and the hash already tells a client for certain whether its cached copy is still correct, so a
/// long freshness window costs nothing it wouldn't already be paying for a stale response.
/// </summary>
public static class CatalogEndpoints
{
    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/v1/applications", GetApplications).RequireAuthorization();
        app.MapGet("/v1/applications/{id:guid}/icon", GetApplicationIcon).RequireAuthorization();
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

    private static async Task<IResult> GetApplicationIcon(
        Guid id,
        HttpContext httpContext,
        AppBridgeDbContext dbContext,
        IIconStorage iconStorage,
        CancellationToken cancellationToken)
    {
        // Tenant-scoped by the same global filter every other query here relies on (ADR-0004): an
        // id belonging to another tenant simply isn't found, the same "404, not a distinguishable
        // error" ADR-0012 §5 already applies elsewhere. No authorization-set check beyond that — an
        // icon is presentation metadata, not the application itself, and API.md's icon section
        // doesn't ask for the RF-011 filter GET /v1/applications applies to the list.
        var correlationId = ResolveCorrelationId(httpContext);
        var instance = $"/v1/applications/{id}/icon";

        var application = await dbContext.Applications.SingleOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (application?.IconRef is null)
        {
            return Results.Problem(CatalogProblems.ApplicationNotFound(instance, correlationId));
        }

        var icon = await iconStorage.ReadAsync(application.IconRef, cancellationToken);
        if (icon is null)
        {
            return Results.Problem(CatalogProblems.ApplicationNotFound(instance, correlationId));
        }

        var etag = ComputeETag(icon.Content);
        httpContext.Response.Headers.ETag = etag;
        // A week: the icon's own content hash is the real freshness check, so a long window costs
        // nothing a client wouldn't already be correctly caching around via If-None-Match.
        httpContext.Response.Headers.CacheControl = "public, max-age=604800, immutable";

        if (RequestHasMatchingETag(httpContext.Request, etag))
        {
            return Results.StatusCode(StatusCodes.Status304NotModified);
        }

        return Results.File(icon.Content, icon.ContentType);
    }

    private static string ResolveCorrelationId(HttpContext httpContext) =>
        httpContext.Items.TryGetValue(Middleware.CorrelationIdMiddleware.HeaderName, out var value) && value is string correlationId
            ? correlationId
            : Guid.NewGuid().ToString();

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
        return $"\"cat-{HashHex(Encoding.UTF8.GetBytes(canonical))}\"";
    }

    private static string ComputeETag(byte[] content) => $"\"icon-{HashHex(content)}\"";

    private static string HashHex(byte[] content) => Convert.ToHexString(SHA256.HashData(content))[..16].ToLowerInvariant();

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
