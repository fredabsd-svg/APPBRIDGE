using AppBridge.ControlPlane.Application.Abstractions.Auditing;
using AppBridge.ControlPlane.Application.Dtos.Auditing;
using System.Security.Claims;

namespace AppBridge.ControlPlane.Infrastructure.Middleware;

public class AuditingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuditingMiddleware> _logger;

    public AuditingMiddleware(RequestDelegate next, ILogger<AuditingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IAuditingService auditingService)
    {
        var originalBody = context.Response.Body;

        try
        {
            using (var memoryStream = new MemoryStream())
            {
                context.Response.Body = memoryStream;

                await _next(context);

                var statusCode = context.Response.StatusCode;
                var isSuccess = statusCode >= 200 && statusCode < 300;

                // Log de auditoria para endpoints autenticados
                if (context.User.Identity?.IsAuthenticated == true)
                {
                    var userIdClaim = context.User.FindFirst("sub");
                    var tenantIdClaim = context.User.FindFirst("tenant_id");
                    var userIdentifierClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)
                        ?? context.User.FindFirst("unique_name");

                    if (tenantIdClaim != null && Guid.TryParse(tenantIdClaim.Value, out var tenantId))
                    {
                        var auditLog = new AuditLogDto
                        {
                            TenantId = tenantId,
                            Category = "Access",
                            ActorUserId = userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId) ? userId : null,
                            ActorIdentifier = userIdentifierClaim?.Value ?? "unknown",
                            Action = $"{context.Request.Method}",
                            ResourceType = "Endpoint",
                            ResourceId = context.Request.Path.ToString(),
                            Description = $"{context.Request.Method} {context.Request.Path}",
                            Result = isSuccess ? "Success" : "Failure",
                            FailureReason = !isSuccess ? $"HTTP {statusCode}" : null,
                            SourceIp = GetClientIp(context),
                            UserAgent = context.Request.Headers["User-Agent"].ToString(),
                            Details = $"{{ \"method\": \"{context.Request.Method}\", \"path\": \"{context.Request.Path}\", \"statusCode\": {statusCode} }}"
                        };

                        try
                        {
                            if (context.Request.Path.StartsWithSegments("/api/health"))
                            {
                                // Não auditar endpoints de health check
                                _logger.LogDebug("Skipping audit for health check endpoint");
                            }
                            else
                            {
                                await auditingService.LogAccessAsync(auditLog, context.RequestAborted);
                            }
                        }
                        catch (InvalidOperationException ex)
                        {
                            _logger.LogError(ex, "Audit logging middleware caught blocker exception");
                            context.Response.StatusCode = 500;
                        }
                    }
                }

                await memoryStream.CopyToAsync(originalBody);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in auditing middleware");
            context.Response.Body = originalBody;
            throw;
        }
        finally
        {
            context.Response.Body = originalBody;
        }
    }

    private string GetClientIp(HttpContext context)
    {
        // Tenta X-Forwarded-For (proxy), depois X-Real-IP, depois RemoteIpAddress
        if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
        {
            var ips = forwardedFor.ToString().Split(',');
            if (ips.Length > 0)
                return ips[0].Trim();
        }

        if (context.Request.Headers.TryGetValue("X-Real-IP", out var realIp))
            return realIp.ToString();

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}

public static class AuditingMiddlewareExtensions
{
    public static IApplicationBuilder UseAuditing(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<AuditingMiddleware>();
    }
}
