using System.Diagnostics;

namespace AppBridge.ControlPlane.Middleware;

public sealed class CorrelationIdMiddleware(
    RequestDelegate next,
    ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetCorrelationId(context);
        context.TraceIdentifier = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["correlationId"] = correlationId
        });

        var timer = Stopwatch.StartNew();
        var failed = false;

        logger.LogInformation(
            "HTTP request started: {Method} {Path}",
            context.Request.Method,
            context.Request.Path);

        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            failed = true;
            logger.LogError(
                exception,
                "HTTP request failed: {Method} {Path}",
                context.Request.Method,
                context.Request.Path);
            throw;
        }
        finally
        {
            timer.Stop();
            logger.LogInformation(
                "HTTP request completed: {Method} {Path} with status {StatusCode} in {ElapsedMilliseconds} ms",
                context.Request.Method,
                context.Request.Path,
                failed ? StatusCodes.Status500InternalServerError : context.Response.StatusCode,
                timer.ElapsedMilliseconds);
        }
    }

    private static string GetCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var values)
            && Guid.TryParse(values.ToString(), out var parsed))
        {
            return parsed.ToString("D");
        }

        return Guid.NewGuid().ToString("D");
    }
}
