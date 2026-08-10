using AppBridge.ControlPlane.Api.Middleware;
using AppBridge.ControlPlane.Domain.Identity;
using AppBridge.ControlPlane.Domain.Tenancy;
using AppBridge.ControlPlane.Domain.Trail;
using AppBridge.ControlPlane.Infrastructure;
using AppBridge.ControlPlane.Infrastructure.Auditing;
using AppBridge.ControlPlane.Infrastructure.Identity;
using AppBridge.ControlPlane.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace AppBridge.ControlPlane.Api.Endpoints;

/// <summary>T-301: <c>POST /v1/auth/session</c> (API.md §2, RF-001, RF-003, RF-036).</summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/v1/auth/session", Login);
        return app;
    }

    private static async Task<IResult> Login(
        LoginRequest request,
        HttpContext httpContext,
        AppBridgeDbContext dbContext,
        TenantContext tenantContext,
        IIdentityProvider identityProvider,
        IAuditWriter auditWriter,
        ISessionTokenIssuer tokenIssuer,
        CancellationToken cancellationToken)
    {
        var correlationId = ResolveCorrelationId(httpContext);

        var identity = await identityProvider.ValidateAsync(request.IdentityToken, cancellationToken);
        if (!identity.IsValid)
        {
            return Results.Problem(AuthProblems.InvalidIdentityToken(correlationId));
        }

        // Tenant isn't ITenantScoped, so this lookup isn't affected by the (still-unset) tenant
        // filter — it's how the filter gets a value to begin with (T-203's "resolved from the
        // token" middleware point, landing here because login is the one place tenant genuinely
        // isn't known yet).
        var tenant = await dbContext.Tenants.SingleOrDefaultAsync(t => t.AdDomain == identity.AdDomain, cancellationToken);
        if (tenant is null)
        {
            // No tenant to attribute the attempt to — every access_event row needs one (only
            // Tenant/SigningCertificate don't, ADR-0011 §1). Logged structurally (RNF-039); not
            // written to access_event, because there's nothing to write it against.
            return Results.Problem(AuthProblems.InvalidIdentityToken(correlationId));
        }

        tenantContext.TenantId = tenant.Id;

        if (tenant.Status != TenantStatus.Active)
        {
            return await DenyAsync(
                auditWriter, NewAccessEvent(tenant.Id, userAccountId: null, request, correlationId, success: false),
                AuthProblems.TenantSuspended(correlationId), correlationId, cancellationToken);
        }

        var user = await dbContext.UserAccounts.SingleOrDefaultAsync(u => u.ExternalSubject == identity.ExternalSubject, cancellationToken);
        if (user is null)
        {
            // MODELO-DE-DADOS.md §7.2: user_account_id null is intentional here — an unrecognized
            // external subject within a known tenant, not a database inconsistency.
            return await DenyAsync(
                auditWriter, NewAccessEvent(tenant.Id, userAccountId: null, request, correlationId, success: false),
                AuthProblems.InvalidIdentityToken(correlationId), correlationId, cancellationToken);
        }

        if (user.Status != UserAccountStatus.Active)
        {
            return await DenyAsync(
                auditWriter, NewAccessEvent(tenant.Id, user.Id, request, correlationId, success: false),
                AuthProblems.UserDisabled(correlationId), correlationId, cancellationToken);
        }

        string[] roles = ["user"]; // RF-075's provider-operator role is MVP-1 — nothing else exists yet.
        var accessToken = tokenIssuer.IssueAccessToken(user, tenant.Id, roles);
        var (refreshTokenValue, refreshTokenHash) = RefreshTokenHasher.GenerateAndHash();
        var accessEvent = NewAccessEvent(tenant.Id, user.Id, request, correlationId, success: true);

        try
        {
            await auditWriter.ExecuteAsync(
                accessEvent,
                ctx =>
                {
                    user.LastLoginAt = DateTimeOffset.UtcNow;
                    ctx.RefreshTokens.Add(new RefreshToken
                    {
                        TenantId = tenant.Id,
                        UserAccountId = user.Id,
                        TokenHash = refreshTokenHash,
                        ExpiresAt = DateTimeOffset.UtcNow.AddDays(30), // PRE-30
                    });
                    return true;
                },
                cancellationToken);
        }
        catch (AuditWriteFailedException)
        {
            return Results.Problem(AuthProblems.AuditUnavailable(correlationId));
        }

        var response = new LoginResponse(
            AccessToken: accessToken.Value,
            ExpiresAt: accessToken.ExpiresAt,
            RefreshToken: refreshTokenValue,
            User: new LoginUser(user.Id, user.DisplayName, new LoginTenant(tenant.Id, tenant.Name), roles));
        return Results.Json(response, statusCode: StatusCodes.Status201Created);
    }

    /// <summary>
    /// ADR-0007 Part 1 applies to every authentication attempt, granted or denied — one code path,
    /// no branch that could forget the transactional guarantee. If recording the denial itself
    /// fails, that failure — not the original denial reason — is what reaches the caller
    /// (AUDIT_UNAVAILABLE), because ADR-0007 doesn't distinguish "couldn't record a grant" from
    /// "couldn't record a denial": either way, the write failed.
    /// </summary>
    private static async Task<IResult> DenyAsync(
        IAuditWriter auditWriter, AccessEvent accessEvent, Microsoft.AspNetCore.Mvc.ProblemDetails deniedProblem,
        string correlationId, CancellationToken cancellationToken)
    {
        try
        {
            await auditWriter.ExecuteAsync(accessEvent, _ => true, cancellationToken);
        }
        catch (AuditWriteFailedException)
        {
            return Results.Problem(AuthProblems.AuditUnavailable(correlationId));
        }

        return Results.Problem(deniedProblem);
    }

    private static AccessEvent NewAccessEvent(
        Guid tenantId, Guid? userAccountId, LoginRequest request, string correlationId, bool success) => new()
    {
        TenantId = tenantId,
        UserAccountId = userAccountId,
        EventType = "login",
        Result = success ? AccessEventResult.Success : AccessEventResult.Failure,
        OccurredAt = DateTimeOffset.UtcNow,
        WorkstationName = request.WorkstationName,
        CorrelationId = Guid.Parse(correlationId),
    };

    private static string ResolveCorrelationId(HttpContext httpContext) =>
        httpContext.Items.TryGetValue(CorrelationIdMiddleware.HeaderName, out var value) && value is string correlationId
            ? correlationId
            : Guid.NewGuid().ToString();
}

public sealed record LoginRequest(string IdentityToken, string? WorkstationName);

public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, string RefreshToken, LoginUser User);

public sealed record LoginUser(Guid Id, string? DisplayName, LoginTenant Tenant, IReadOnlyList<string> Roles);

public sealed record LoginTenant(Guid Id, string Name);
