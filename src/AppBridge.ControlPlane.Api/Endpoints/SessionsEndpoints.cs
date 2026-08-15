using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AppBridge.ControlPlane.Domain.Sessions;
using AppBridge.ControlPlane.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AppBridge.ControlPlane.Api.Endpoints;

/// <summary>
/// T-603: <c>GET /v1/sessions/me</c> (API.md §4, RF-024, RF-027) — the caller's own active
/// sessions, "para o launcher indicar estado e apoiar a reconexão". The last task of E-06: every
/// active <see cref="Session"/> a launch could have created is finally observable through the API,
/// not just through <c>psql</c> during manual verification (T-601/T-602).
///
/// RF-027 (auto-reconnect after a network drop) is MVP-1 — this endpoint only has to expose the
/// data that feature will eventually consume, not implement reconnection logic itself, the same
/// "endpoint ships, later feature reads it" relationship <c>GET /v1/applications</c>'s
/// <c>available</c> field has with a health signal nothing produces yet.
///
/// No <c>applicationId</c>/host FQDN in the response: <see cref="Session"/> doesn't record which
/// application launched it (only <c>Launch</c> does, T-204) — inventing that join here would be
/// scope RF-024's actual ask ("indicar estado") never asked for — and RNF-043 forbids leaking
/// internal host detail to the client either way, the same reasoning
/// <c>LaunchEndpoints</c>'s <c>LaunchResponseHost</c> already established (reused here rather than
/// duplicated).
/// </summary>
public static class SessionsEndpoints
{
    public static IEndpointRouteBuilder MapSessionsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/v1/sessions/me", GetMySessions).RequireAuthorization();
        return app;
    }

    private static async Task<IResult> GetMySessions(
        HttpContext httpContext, AppBridgeDbContext dbContext, CancellationToken cancellationToken)
    {
        var userAccountId = Guid.Parse(httpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

        // Tenant isolation is automatic (ADR-0004) — no explicit tenant filter needed, same shape
        // as every other authenticated read in this codebase (GetApplications, T-402).
        var sessions = await dbContext.Sessions
            .Where(s => s.UserAccountId == userAccountId && s.EndedAt == null)
            .OrderBy(s => s.StartedAt)
            .ToListAsync(cancellationToken);

        var items = sessions
            .Select(s => new ActiveSession(s.Id, s.StartedAt, s.LastSeenAt, new LaunchResponseHost("Servidor de aplicativos")))
            .ToList();

        return Results.Ok(new SessionsResponse(items));
    }
}

public sealed record SessionsResponse(IReadOnlyList<ActiveSession> Items);

public sealed record ActiveSession(Guid Id, DateTimeOffset StartedAt, DateTimeOffset LastSeenAt, LaunchResponseHost Host);
