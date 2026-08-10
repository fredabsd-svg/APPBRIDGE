using System.Diagnostics;

namespace AppBridge.ControlPlane.Api.Middleware;

/// <summary>
/// Resolves the correlation id for a request (API.md §1.1) and attaches it to the response
/// header and to every log line written while the request is in flight (RNF-039).
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context, ILogger<CorrelationIdMiddleware> logger)
    {
        var correlationId = ResolveCorrelationId(context);
        context.Items[HeaderName] = correlationId;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            // These two lines are the traceability guarantee itself (RNF-039), not decoration:
            // without something in the request path actually logging, the scope above never
            // reaches an output line and "rastreável ponta a ponta pelo log" would be untrue.
            var startedAt = Stopwatch.GetTimestamp();
            logger.LogInformation(
                "Request starting {Method} {Path}", context.Request.Method, context.Request.Path);

            await next(context);

            logger.LogInformation(
                "Request finished {Method} {Path} responded {StatusCode} in {ElapsedMs}ms",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var values)
            && Guid.TryParse(values.ToString(), out var parsed))
        {
            return parsed.ToString();
        }

        return Guid.NewGuid().ToString();
    }
}
