using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AppBridge.ControlPlane.Api.HealthChecks;

/// <summary>
/// Writes the /v1/health response. Reports which dependency is unhealthy so operations can act,
/// but never leaks host names, paths or exceptions (RNF-043) — that detail belongs in the
/// structured log, correlated by CorrelationIdMiddleware, not in a response any client can read.
/// </summary>
public static class HealthCheckResponseWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = false };

    public static Task Write(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var payload = new
        {
            status = report.Status.ToString().ToLowerInvariant(),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString().ToLowerInvariant(),
            }),
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(payload, SerializerOptions));
    }
}
