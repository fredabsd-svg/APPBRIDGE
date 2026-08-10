using AppBridge.ControlPlane.Api.HealthChecks;
using AppBridge.ControlPlane.Api.Middleware;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Structured JSON logs with scopes, so CorrelationIdMiddleware's scope shows up on every line
// (RNF-039 — a launch must be traceable end to end through the log).
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.UseUtcTimestamp = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
});

builder.Services.AddOpenApi();

// "self" proves the wiring; dependency checks (PostgreSQL, AD DS, signing certificate,
// ISessionBackend, disk space) are added by their owning task as each dependency comes online —
// T-202, T-301, T-502, T-503 — rather than stubbed here ahead of the code that would back them.
builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<CorrelationIdMiddleware>();

app.MapHealthChecks("/v1/health", new HealthCheckOptions
{
    ResponseWriter = HealthCheckResponseWriter.Write,
});

app.Run();

public partial class Program
{
}
