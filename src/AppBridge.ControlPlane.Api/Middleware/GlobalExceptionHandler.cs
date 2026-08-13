using AppBridge.ControlPlane.Api.Endpoints;
using Microsoft.AspNetCore.Diagnostics;

namespace AppBridge.ControlPlane.Api.Middleware;

/// <summary>
/// T-505: the single place an exception that reaches the top of the pipeline turns into a
/// response, replacing ASP.NET Core's own defaults — the Development environment's exception page
/// (a full stack trace, including source file paths, in the response body) and, without that, a
/// bare unstyled 500. Both violate RNF-043; the second also isn't even attributable via
/// <c>correlationId</c>. Registered so it wins over the automatic developer exception page
/// (<c>Program.cs</c>: <c>AddExceptionHandler</c> + <c>AddProblemDetails</c> before
/// <c>UseExceptionHandler()</c> suppresses it) — MVP-0's only <c>IIdentityProvider</c>
/// implementation is registered exclusively under Development (ADR-0017 §5), so this is not just a
/// test-environment concern: the actual dogfood instance runs in Development too, meaning this
/// leak was reachable in what ships, not only in local debugging.
///
/// <see cref="BadHttpRequestException"/> — what minimal API's own request-body binding throws for
/// malformed JSON or a missing required property, before any endpoint handler runs — maps to
/// <c>400 MALFORMED_REQUEST</c> (API.md §9); everything else maps to <c>500 INTERNAL_ERROR</c>. The
/// real exception is logged here, server-side, with the same <c>correlationId</c> the caller
/// receives — the caller never sees more than that id.
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var correlationId = httpContext.Items.TryGetValue(CorrelationIdMiddleware.HeaderName, out var value)
            && value is string resolvedCorrelationId
                ? resolvedCorrelationId
                : Guid.NewGuid().ToString();

        logger.LogError(
            exception,
            "Unhandled exception for {Method} {Path}",
            httpContext.Request.Method,
            httpContext.Request.Path);

        var problem = exception is BadHttpRequestException
            ? GlobalProblems.MalformedRequest(httpContext.Request.Path, correlationId)
            : GlobalProblems.InternalError(httpContext.Request.Path, correlationId);

        httpContext.Response.StatusCode = problem.Status!.Value;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
