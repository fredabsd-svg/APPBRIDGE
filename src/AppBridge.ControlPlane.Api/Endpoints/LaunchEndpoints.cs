using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AppBridge.ControlPlane.Api.Idempotency;
using AppBridge.ControlPlane.Api.Middleware;
using AppBridge.ControlPlane.Domain.Catalog;
using AppBridge.ControlPlane.Domain.Trail;
using AppBridge.ControlPlane.Infrastructure;
using AppBridge.ControlPlane.Infrastructure.Auditing;
using AppBridge.ControlPlane.Infrastructure.Authorization;
using AppBridge.ControlPlane.Infrastructure.Rdp;
using AppBridge.ControlPlane.Infrastructure.Sessions;
using AppBridge.ControlPlane.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AppBridge.ControlPlane.Api.Endpoints;

/// <summary>
/// T-504: <c>POST /v1/launches</c> (API.md §4, RF-018..RF-021, RF-025, RF-037, RF-039). The
/// "coração do produto" endpoint — the first real consumer of <c>IAuthorizationService</c> (T-304),
/// <c>ISessionBackend</c> (T-503), <c>IRdpDescriptorBuilder</c> (T-501) and <c>IRdpFileSigner</c>
/// (T-502) together.
///
/// Every outcome — granted or denied, for any reason — writes exactly one <see cref="Launch"/> row
/// through <see cref="IAuditWriter"/> (RF-037 is in ADR-0007 Part 1's blocking list, same as
/// RF-036/RF-039/RF-041/RF-042; the same "one code path, no branch that could forget the
/// transactional guarantee" discipline T-301's login denials already established). The one
/// exception is <c>AUDIT_UNAVAILABLE</c> itself: when the write fails, nothing was persisted, so
/// there is nothing to replay on the caller's next attempt — see the idempotency handling below.
/// </summary>
public static class LaunchEndpoints
{
    /// <summary>PRE-07 — how long the signed .rdp (and, not by coincidence, the idempotency window it shares, ADR-0012 §3) stays valid.</summary>
    private static readonly TimeSpan RdpTtl = TimeSpan.FromSeconds(60);

    public static IEndpointRouteBuilder MapLaunchEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/v1/launches", CreateLaunch).RequireAuthorization();
        return app;
    }

    private static async Task<IResult> CreateLaunch(
        LaunchRequest request,
        [FromHeader(Name = "Idempotency-Key")] Guid idempotencyKey,
        HttpContext httpContext,
        AppBridgeDbContext dbContext,
        ITenantContext tenantContext,
        IAuthorizationService authorizationService,
        ISessionBackend sessionBackend,
        IRdpDescriptorBuilder descriptorBuilder,
        IRdpFileSigner rdpFileSigner,
        IAuditWriter auditWriter,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        const string instance = "/v1/launches";
        var correlationId = ResolveCorrelationId(httpContext);
        var tenantId = tenantContext.TenantId!.Value; // set by TenantResolutionMiddleware — this route is [Authorize]-gated
        var userAccountId = Guid.Parse(httpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
        var requestHash = HashRequest(request);

        if (idempotencyStore.TryGet(tenantId, idempotencyKey, out var cached))
        {
            if (cached!.RequestBodyHash == requestHash)
            {
                return Results.Text(cached.Body, cached.ContentType, statusCode: cached.StatusCode);
            }

            return Respond(StatusCodes.Status409Conflict, LaunchProblems.IdempotencyConflict(instance, correlationId));
        }

        if (!TryParsePurpose(request.Purpose, out var purpose))
        {
            // A malformed request is a client bug, not a business outcome to remember for 60 s —
            // deliberately not cached, unlike everything past this point.
            return Respond(StatusCodes.Status400BadRequest, LaunchProblems.InvalidPurpose(instance, correlationId));
        }

        var application = await dbContext.Applications
            .SingleOrDefaultAsync(a => a.Id == request.ApplicationId && a.Status == ApplicationStatus.Published, cancellationToken);
        if (application is null)
        {
            // No Launch row possible here — ApplicationId doesn't resolve to a row the composite
            // FK could even point at. Still cached: "does this application exist" won't flip
            // within the 60 s window, so a retry gets the same, correct answer without a second
            // round trip through this same check.
            return await RespondAndCacheAsync(
                idempotencyStore, tenantId, idempotencyKey, requestHash,
                StatusCodes.Status404NotFound, LaunchProblems.ApplicationNotFound(instance, correlationId));
        }

        var hasPermission = await authorizationService.HasActivePermissionAsync(userAccountId, application.Id, cancellationToken);
        if (!hasPermission)
        {
            return await DenyAndAuditAsync(
                auditWriter, idempotencyStore, tenantId, idempotencyKey, requestHash,
                NewLaunch(tenantId, userAccountId, application.Id, request, purpose, correlationId, httpContext, LaunchOutcome.DeniedPermission, "permission_revoked"),
                StatusCodes.Status403Forbidden, LaunchProblems.PermissionRevoked(instance, correlationId),
                instance, correlationId, cancellationToken);
        }

        var host = await sessionBackend.ResolveHostAsync(application.Id, cancellationToken);
        if (host is null)
        {
            return await DenyAndAuditAsync(
                auditWriter, idempotencyStore, tenantId, idempotencyKey, requestHash,
                NewLaunch(tenantId, userAccountId, application.Id, request, purpose, correlationId, httpContext, LaunchOutcome.DeniedHostUnavailable, "host_unavailable"),
                StatusCodes.Status422UnprocessableEntity, LaunchProblems.ApplicationUnavailable(instance, correlationId),
                instance, correlationId, cancellationToken);
        }

        var connectionParameters = await sessionBackend.BuildConnectionDescriptorAsync(host, application, cancellationToken);
        var unsignedRdp = descriptorBuilder.Build(connectionParameters);

        string signedRdp;
        try
        {
            signedRdp = await rdpFileSigner.SignAsync(unsignedRdp, cancellationToken);
        }
        catch (RdpSigningFailedException)
        {
            return await DenyAndAuditAsync(
                auditWriter, idempotencyStore, tenantId, idempotencyKey, requestHash,
                NewLaunch(tenantId, userAccountId, application.Id, request, purpose, correlationId, httpContext, LaunchOutcome.ErrorSigning, "signing_failed"),
                StatusCodes.Status503ServiceUnavailable, LaunchProblems.SigningUnavailable(instance, correlationId),
                instance, correlationId, cancellationToken);
        }

        var expiresAt = DateTimeOffset.UtcNow.Add(RdpTtl);
        var launch = NewLaunch(tenantId, userAccountId, application.Id, request, purpose, correlationId, httpContext, LaunchOutcome.Granted, denialReason: null);
        launch.RdpExpiresAt = expiresAt;

        try
        {
            await auditWriter.ExecuteAsync(launch, _ => true, cancellationToken);
        }
        catch (AuditWriteFailedException)
        {
            // Nothing persisted — unlike every other path above, there is no Launch row a retry
            // could duplicate, so this is the one response deliberately not cached: the caller
            // should get a fresh attempt, not a frozen failure from a transient write error.
            return Respond(StatusCodes.Status503ServiceUnavailable, LaunchProblems.AuditUnavailable(instance, correlationId));
        }

        var response = new LaunchResponse(
            LaunchId: launch.Id,
            // No real session tracking exists yet (SessionRegistry is T-601) — always false rather
            // than a guess this code has no data to back up.
            SessionReused: false,
            RdpFile: Convert.ToBase64String(Encoding.UTF8.GetBytes(signedRdp)),
            ExpiresAt: expiresAt,
            // A generic label, not host.Fqdn — RNF-043 forbids leaking internal host detail to the
            // end user; the real address is inside the signed .rdp, not in this JSON.
            Host: new LaunchResponseHost("Servidor de aplicativos"),
            CorrelationId: correlationId);

        return await RespondAndCacheAsync(idempotencyStore, tenantId, idempotencyKey, requestHash, StatusCodes.Status201Created, response);
    }

    private static Launch NewLaunch(
        Guid tenantId, Guid userAccountId, Guid applicationId, LaunchRequest request, LaunchPurpose purpose,
        string correlationId, HttpContext httpContext, LaunchOutcome outcome, string? denialReason) => new()
    {
        TenantId = tenantId,
        UserAccountId = userAccountId,
        ApplicationId = applicationId,
        RequestedAt = DateTimeOffset.UtcNow,
        Outcome = outcome,
        DenialReason = denialReason,
        SourceIp = httpContext.Connection.RemoteIpAddress?.ToString(),
        WorkstationName = request.WorkstationName,
        RdpExpiresAt = DateTimeOffset.UtcNow, // overwritten by the caller on the Granted path
        CorrelationId = Guid.Parse(correlationId),
        Purpose = purpose,
    };

    private static async Task<IResult> DenyAndAuditAsync(
        IAuditWriter auditWriter, IIdempotencyStore idempotencyStore, Guid tenantId, Guid idempotencyKey, string requestHash,
        Launch launch, int statusCode, LaunchProblemBody problem, string instance, string correlationId, CancellationToken cancellationToken)
    {
        try
        {
            await auditWriter.ExecuteAsync(launch, _ => true, cancellationToken);
        }
        catch (AuditWriteFailedException)
        {
            return Respond(StatusCodes.Status503ServiceUnavailable, LaunchProblems.AuditUnavailable(instance, correlationId));
        }

        return await RespondAndCacheAsync(idempotencyStore, tenantId, idempotencyKey, requestHash, statusCode, problem);
    }

    private static async Task<IResult> RespondAndCacheAsync(
        IIdempotencyStore idempotencyStore, Guid tenantId, Guid idempotencyKey, string requestHash, int statusCode, object body)
    {
        var (contentType, json) = Serialize(statusCode, body);
        idempotencyStore.Set(tenantId, idempotencyKey, new CachedIdempotentResponse(requestHash, statusCode, contentType, json));
        return Results.Text(json, contentType, statusCode: statusCode);
    }

    private static IResult Respond(int statusCode, object body)
    {
        var (contentType, json) = Serialize(statusCode, body);
        return Results.Text(json, contentType, statusCode: statusCode);
    }

    private static (string ContentType, string Json) Serialize(int statusCode, object body)
    {
        var contentType = body is LaunchProblemBody ? "application/problem+json" : "application/json";
        return (contentType, JsonSerializer.Serialize(body, JsonSerializerOptions.Web));
    }

    private static bool TryParsePurpose(string value, out LaunchPurpose purpose)
    {
        switch (value)
        {
            case "user_initiated":
                purpose = LaunchPurpose.UserInitiated;
                return true;
            case "prelaunch":
                purpose = LaunchPurpose.Prelaunch;
                return true;
            default:
                purpose = default;
                return false;
        }
    }

    private static string HashRequest(LaunchRequest request)
    {
        var canonical = $"{request.ApplicationId}:{request.Purpose}:{request.WorkstationName}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static string ResolveCorrelationId(HttpContext httpContext) =>
        httpContext.Items.TryGetValue(CorrelationIdMiddleware.HeaderName, out var value) && value is string correlationId
            ? correlationId
            : Guid.NewGuid().ToString();
}

public sealed record LaunchRequest
{
    public required Guid ApplicationId { get; init; }

    public required string Purpose { get; init; }

    public string? WorkstationName { get; init; }
}

public sealed record LaunchResponse(
    Guid LaunchId, bool SessionReused, string RdpFile, DateTimeOffset ExpiresAt, LaunchResponseHost Host, string CorrelationId);

public sealed record LaunchResponseHost(string DisplayName);
