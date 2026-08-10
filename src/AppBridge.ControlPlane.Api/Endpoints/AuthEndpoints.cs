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

/// <summary>
/// T-301: <c>POST /v1/auth/session</c>. T-303: <c>POST /v1/auth/refresh</c>,
/// <c>POST /v1/auth/logout</c> (API.md §2, RF-001, RF-003, RF-004, RF-006, RF-036). Token
/// persistence in the Windows Credential Manager (RF-005) is the launcher's job — T-803, a WinUI
/// project that doesn't exist in this repository yet — not this file's.
/// </summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/v1/auth/session", Login);
        app.MapPost("/v1/auth/refresh", Refresh);
        app.MapPost("/v1/auth/logout", Logout);
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
        const string instance = "/v1/auth/session";
        var correlationId = ResolveCorrelationId(httpContext);

        var identity = await identityProvider.ValidateAsync(request.IdentityToken, cancellationToken);
        if (!identity.IsValid)
        {
            return Results.Problem(AuthProblems.InvalidIdentityToken(instance, correlationId));
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
            return Results.Problem(AuthProblems.InvalidIdentityToken(instance, correlationId));
        }

        tenantContext.TenantId = tenant.Id;

        if (tenant.Status != TenantStatus.Active)
        {
            return await DenyLoginAsync(
                auditWriter, NewLoginEvent(tenant.Id, userAccountId: null, request.WorkstationName, correlationId, success: false),
                AuthProblems.TenantSuspended(instance, correlationId), instance, correlationId, cancellationToken);
        }

        var user = await dbContext.UserAccounts.SingleOrDefaultAsync(u => u.ExternalSubject == identity.ExternalSubject, cancellationToken);
        if (user is null)
        {
            // MODELO-DE-DADOS.md §7.2: user_account_id null is intentional here — an unrecognized
            // external subject within a known tenant, not a database inconsistency.
            return await DenyLoginAsync(
                auditWriter, NewLoginEvent(tenant.Id, userAccountId: null, request.WorkstationName, correlationId, success: false),
                AuthProblems.InvalidIdentityToken(instance, correlationId), instance, correlationId, cancellationToken);
        }

        if (user.Status != UserAccountStatus.Active)
        {
            return await DenyLoginAsync(
                auditWriter, NewLoginEvent(tenant.Id, user.Id, request.WorkstationName, correlationId, success: false),
                AuthProblems.UserDisabled(instance, correlationId), instance, correlationId, cancellationToken);
        }

        string[] roles = ["user"]; // RF-075's provider-operator role is MVP-1 — nothing else exists yet.
        var accessToken = tokenIssuer.IssueAccessToken(user, tenant.Id, roles);
        var (refreshTokenValue, refreshTokenHash) = RefreshTokenHasher.GenerateAndHash();
        var accessEvent = NewLoginEvent(tenant.Id, user.Id, request.WorkstationName, correlationId, success: true);

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
            return Results.Problem(AuthProblems.AuditUnavailable(instance, correlationId));
        }

        var response = new SessionResponse(
            AccessToken: accessToken.Value,
            ExpiresAt: accessToken.ExpiresAt,
            RefreshToken: refreshTokenValue,
            User: new SessionUser(user.Id, user.DisplayName, new SessionTenant(tenant.Id, tenant.Name), roles));
        return Results.Json(response, statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> Refresh(
        RefreshTokenRequest request,
        HttpContext httpContext,
        AppBridgeDbContext dbContext,
        TenantContext tenantContext,
        ISessionTokenIssuer tokenIssuer,
        CancellationToken cancellationToken)
    {
        const string instance = "/v1/auth/refresh";
        var correlationId = ResolveCorrelationId(httpContext);

        var refreshToken = await FindRefreshTokenAsync(dbContext, request.RefreshToken, cancellationToken);
        if (refreshToken is null || refreshToken.RevokedAt is not null || refreshToken.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            // API.md: "401 REFRESH_EXPIRED obriga novo login" — covers not-found, already-revoked
            // and genuinely-expired alike; none of the three should be distinguishable to the
            // caller (same anti-enumeration reasoning as ADR-0012 §5).
            return Results.Problem(AuthProblems.RefreshExpired(instance, correlationId));
        }

        tenantContext.TenantId = refreshToken.TenantId;

        var tenant = await dbContext.Tenants.SingleAsync(t => t.Id == refreshToken.TenantId, cancellationToken);
        if (tenant.Status != TenantStatus.Active)
        {
            return Results.Problem(AuthProblems.TenantSuspended(instance, correlationId));
        }

        var user = await dbContext.UserAccounts.SingleAsync(u => u.Id == refreshToken.UserAccountId, cancellationToken);
        if (user.Status != UserAccountStatus.Active)
        {
            // A refresh token outstanding for a user disabled after it was issued must not renew —
            // otherwise disabling an account wouldn't actually stop it from working.
            return Results.Problem(AuthProblems.UserDisabled(instance, correlationId));
        }

        string[] roles = ["user"];
        var accessToken = tokenIssuer.IssueAccessToken(user, tenant.Id, roles);
        var (newRefreshTokenValue, newRefreshTokenHash) = RefreshTokenHasher.GenerateAndHash();

        // Rotation: the presented token is revoked the moment a new one is issued from it, so
        // reusing an already-exchanged refresh token — the signature of a stolen one — fails from
        // here on (T-303's own reason for existing, not audited: ADR-0007 Part 1 does not list
        // RF-004 among the blocking events, and MODELO-DE-DADOS.md §7.2 doesn't categorize refresh
        // as an access_event type the way it does login and logout).
        refreshToken.RevokedAt = DateTimeOffset.UtcNow;
        dbContext.RefreshTokens.Add(new RefreshToken
        {
            TenantId = tenant.Id,
            UserAccountId = user.Id,
            TokenHash = newRefreshTokenHash,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30), // PRE-30
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = new SessionResponse(
            AccessToken: accessToken.Value,
            ExpiresAt: accessToken.ExpiresAt,
            RefreshToken: newRefreshTokenValue,
            User: new SessionUser(user.Id, user.DisplayName, new SessionTenant(tenant.Id, tenant.Name), roles));
        return Results.Json(response, statusCode: StatusCodes.Status200OK);
    }

    private static async Task<IResult> Logout(
        RefreshTokenRequest request,
        HttpContext httpContext,
        AppBridgeDbContext dbContext,
        TenantContext tenantContext,
        IAuditWriter auditWriter,
        CancellationToken cancellationToken)
    {
        const string instance = "/v1/auth/logout";
        var correlationId = ResolveCorrelationId(httpContext);

        var refreshToken = await FindRefreshTokenAsync(dbContext, request.RefreshToken, cancellationToken);

        // RF-006: logout succeeds from the caller's perspective either way — an unknown or
        // already-revoked token means the desired end state (no valid token) already holds.
        // Nothing to gain by distinguishing "never existed" from "already logged out" (same
        // anti-enumeration reasoning as ADR-0012 §5), and no tenant to attribute an access_event to
        // when the token was never found.
        if (refreshToken is null || refreshToken.RevokedAt is not null)
        {
            return Results.NoContent();
        }

        tenantContext.TenantId = refreshToken.TenantId;

        // MODELO-DE-DADOS.md §7.2 categorizes logout as an access_event type alongside
        // authentication — unlike refresh, this one goes through IAuditWriter.
        var accessEvent = new AccessEvent
        {
            TenantId = refreshToken.TenantId,
            UserAccountId = refreshToken.UserAccountId,
            EventType = "logout",
            Result = AccessEventResult.Success,
            OccurredAt = DateTimeOffset.UtcNow,
            CorrelationId = Guid.Parse(correlationId),
        };

        try
        {
            await auditWriter.ExecuteAsync(accessEvent, _ => refreshToken.RevokedAt = DateTimeOffset.UtcNow, cancellationToken);
        }
        catch (AuditWriteFailedException)
        {
            return Results.Problem(AuthProblems.AuditUnavailable(instance, correlationId));
        }

        return Results.NoContent();
    }

    /// <summary>
    /// The presented token is the only thing identifying the tenant at this point — the same
    /// bootstrapping problem login has with identity, except here the row itself IS tenant-scoped
    /// (unlike <c>Tenant</c>), so this is a genuine, explicit use of <c>IgnoreQueryFilters()</c>
    /// (ADR-0004 item 7): there is no tenant to scope by until this exact row is found.
    /// </summary>
    private static Task<RefreshToken?> FindRefreshTokenAsync(
        AppBridgeDbContext dbContext, string rawToken, CancellationToken cancellationToken)
    {
        var hash = RefreshTokenHasher.Hash(rawToken);
        return dbContext.RefreshTokens.IgnoreQueryFilters()
            .SingleOrDefaultAsync(rt => rt.TokenHash == hash, cancellationToken);
    }

    /// <summary>
    /// ADR-0007 Part 1 applies to every authentication attempt, granted or denied — one code path,
    /// no branch that could forget the transactional guarantee. If recording the denial itself
    /// fails, that failure — not the original denial reason — is what reaches the caller
    /// (AUDIT_UNAVAILABLE), because ADR-0007 doesn't distinguish "couldn't record a grant" from
    /// "couldn't record a denial": either way, the write failed.
    /// </summary>
    private static async Task<IResult> DenyLoginAsync(
        IAuditWriter auditWriter, AccessEvent accessEvent, Microsoft.AspNetCore.Mvc.ProblemDetails deniedProblem,
        string instance, string correlationId, CancellationToken cancellationToken)
    {
        try
        {
            await auditWriter.ExecuteAsync(accessEvent, _ => true, cancellationToken);
        }
        catch (AuditWriteFailedException)
        {
            return Results.Problem(AuthProblems.AuditUnavailable(instance, correlationId));
        }

        return Results.Problem(deniedProblem);
    }

    private static AccessEvent NewLoginEvent(
        Guid tenantId, Guid? userAccountId, string? workstationName, string correlationId, bool success) => new()
    {
        TenantId = tenantId,
        UserAccountId = userAccountId,
        EventType = "login",
        Result = success ? AccessEventResult.Success : AccessEventResult.Failure,
        OccurredAt = DateTimeOffset.UtcNow,
        WorkstationName = workstationName,
        CorrelationId = Guid.Parse(correlationId),
    };

    private static string ResolveCorrelationId(HttpContext httpContext) =>
        httpContext.Items.TryGetValue(CorrelationIdMiddleware.HeaderName, out var value) && value is string correlationId
            ? correlationId
            : Guid.NewGuid().ToString();
}

public sealed record LoginRequest
{
    public required string IdentityToken { get; init; }

    public string? WorkstationName { get; init; }
}

public sealed record RefreshTokenRequest
{
    public required string RefreshToken { get; init; }
}

public sealed record SessionResponse(string AccessToken, DateTimeOffset ExpiresAt, string RefreshToken, SessionUser User);

public sealed record SessionUser(Guid Id, string? DisplayName, SessionTenant Tenant, IReadOnlyList<string> Roles);

public sealed record SessionTenant(Guid Id, string Name);
