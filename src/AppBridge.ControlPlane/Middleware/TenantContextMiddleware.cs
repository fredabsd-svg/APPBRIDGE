using System.Security.Claims;
using AppBridge.ControlPlane.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AppBridge.ControlPlane.Middleware;

public sealed class TenantContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        TenantContext tenantContext,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var endpointAllowsAnonymous = context.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is not null;
        if (context.User.Identity?.IsAuthenticated == true && !endpointAllowsAnonymous)
        {
            var tenantClaim = context.User.FindFirstValue("tenant_id");
            var userClaim = context.User.FindFirstValue("sub");
            var sessionClaim = context.User.FindFirstValue("sid");
            if (!Guid.TryParse(tenantClaim, out var tenantId)
                || tenantId == Guid.Empty
                || !Guid.TryParse(userClaim, out var userId)
                || userId == Guid.Empty
                || !Guid.TryParse(sessionClaim, out var sessionId)
                || sessionId == Guid.Empty)
            {
                await WriteExpiredSessionAsync(context, cancellationToken);
                return;
            }

            tenantContext.Bind(tenantId);
            var now = DateTimeOffset.UtcNow;
            var active = await dbContext.AuthenticationSessions
                .AsNoTracking()
                .AnyAsync(session => session.Id == sessionId
                    && session.UserAccountId == userId
                    && session.RevokedAt == null
                    && session.ExpiresAt > now
                    && session.AbsoluteExpiresAt > now,
                    cancellationToken);
            if (!active)
            {
                await WriteExpiredSessionAsync(context, cancellationToken);
                return;
            }
        }

        await next(context);
    }

    private static async Task WriteExpiredSessionAsync(HttpContext context, CancellationToken cancellationToken)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/problem+json";
        var correlationId = Guid.TryParse(context.TraceIdentifier, out var parsed)
            ? parsed
            : Guid.CreateVersion7();
        var problem = new ProblemDetails
        {
            Type = "urn:appbridge:problem:session-expired",
            Title = "Sua sessão expirou. Entre novamente.",
            Status = StatusCodes.Status401Unauthorized,
            Detail = "Sua sessão expirou ou foi encerrada. Entre novamente.",
            Instance = context.Request.Path
        };
        problem.Extensions["appbridgeCode"] = "SESSION_EXPIRED";
        problem.Extensions["correlationId"] = correlationId;
        await context.Response.WriteAsJsonAsync(
            problem,
            options: null,
            contentType: "application/problem+json",
            cancellationToken: cancellationToken);
    }
}
