using System.Text;
using AppBridge.ControlPlane.Api.Endpoints;
using AppBridge.ControlPlane.Api.HealthChecks;
using AppBridge.ControlPlane.Api.Identity;
using AppBridge.ControlPlane.Api.Middleware;
using AppBridge.ControlPlane.Infrastructure;
using AppBridge.ControlPlane.Infrastructure.Auditing;
using AppBridge.ControlPlane.Infrastructure.Authorization;
using AppBridge.ControlPlane.Infrastructure.Identity;
using AppBridge.ControlPlane.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

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

var dbConnectionString = builder.Configuration["APPBRIDGE_DB_CONNECTION"]
    ?? throw new InvalidOperationException(
        "Set APPBRIDGE_DB_CONNECTION before running the Control Plane (see docs/SETUP-DEV.md).");
builder.Services.AddDbContext<AppBridgeDbContext>(options => options.UseNpgsql(dbConnectionString));

// Scoped and settable so the login endpoint (the one place tenant genuinely isn't known yet,
// ADR-0017) can resolve and set it; everything else reads it through the read-only ITenantContext
// (T-203) — the interface's shape is what keeps every other consumer from writing to it by accident.
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());

builder.Services.AddScoped<IAuditWriter, AuditWriter>();
builder.Services.AddScoped<IAuthorizationService, AuthorizationService>();

var jwtSigningKey = builder.Configuration["APPBRIDGE_JWT_SIGNING_KEY"]
    ?? throw new InvalidOperationException(
        "Set APPBRIDGE_JWT_SIGNING_KEY before running the Control Plane (see docs/SETUP-DEV.md).");
var jwtSigningOptions = new JwtSigningOptions { SigningKey = jwtSigningKey };
builder.Services.AddSingleton<ISessionTokenIssuer>(new JwtSessionTokenIssuer(jwtSigningOptions));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Default is true, which silently renames some short claim types (e.g. "sub") to long
        // legacy .NET URIs on validation — disabled so a consumer reading claims sees exactly what
        // JwtSessionTokenIssuer wrote.
        options.MapInboundClaims = false;

        // No issuer/audience validation yet — single Control Plane instance, nothing else verifies
        // this token (ADR-0017 §1). Revisit if a second service ever needs to.
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
        };
    });
builder.Services.AddAuthorization();

// ADR-0017 §5: no real Entra ID/AD DS integration exists yet (E-01 has no hardware). Registered
// only under Development — outside it, IIdentityProvider has no implementation at all, so DI
// resolution fails loudly rather than silently running without real identity verification.
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<IIdentityProvider, DevIdentityProvider>();
}

// "self" always proves the wiring. PostgreSQL joins in T-301 — AppBridgeDbContext finally has a
// real runtime consumer (POST /v1/auth/session); AD DS, signing certificate and ISessionBackend
// still wait for T-502/T-503, each added by its own owning task rather than stubbed ahead of the
// code that would back it.
builder.Services.AddHealthChecks()
    .AddCheck<PostgresHealthCheck>("postgresql");

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<CorrelationIdMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/v1/health", new HealthCheckOptions
{
    ResponseWriter = HealthCheckResponseWriter.Write,
});

app.MapAuthEndpoints();

app.Run();

public partial class Program
{
}
