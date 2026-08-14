using System.Text;
using AppBridge.ControlPlane.Api.BackgroundServices;
using AppBridge.ControlPlane.Api.Endpoints;
using AppBridge.ControlPlane.Api.HealthChecks;
using AppBridge.ControlPlane.Api.Identity;
using AppBridge.ControlPlane.Api.Idempotency;
using AppBridge.ControlPlane.Api.Middleware;
using AppBridge.ControlPlane.Infrastructure;
using AppBridge.ControlPlane.Infrastructure.Auditing;
using AppBridge.ControlPlane.Infrastructure.Authorization;
using AppBridge.ControlPlane.Infrastructure.Catalog;
using AppBridge.ControlPlane.Infrastructure.Identity;
using AppBridge.ControlPlane.Infrastructure.Rdp;
using AppBridge.ControlPlane.Infrastructure.Sessions;
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
builder.Services.AddSingleton<IRdpDescriptorBuilder, RdpDescriptorBuilder>();

var jwtSigningKey = builder.Configuration["APPBRIDGE_JWT_SIGNING_KEY"]
    ?? throw new InvalidOperationException(
        "Set APPBRIDGE_JWT_SIGNING_KEY before running the Control Plane (see docs/SETUP-DEV.md).");
var jwtSigningOptions = new JwtSigningOptions { SigningKey = jwtSigningKey };
builder.Services.AddSingleton<ISessionTokenIssuer>(new JwtSessionTokenIssuer(jwtSigningOptions));

// PD-03 (API.md §3/§11): the icon binary lives on disk, referenced by Application.IconRef, not in
// the database.
var iconStoragePath = builder.Configuration["APPBRIDGE_ICON_STORAGE_PATH"]
    ?? throw new InvalidOperationException(
        "Set APPBRIDGE_ICON_STORAGE_PATH before running the Control Plane (see docs/SETUP-DEV.md).");
builder.Services.AddSingleton<IIconStorage>(new FileSystemIconStorage(new IconStorageOptions { RootPath = iconStoragePath }));

// ADR-0009: the .rdp is signed by invoking rdpsign.exe, a real Windows tool — not something this
// codebase reimplements. The thumbprint identifies which certificate to use; it isn't a secret
// (the private key never leaves the machine's certificate store).
var rdpSigningThumbprint = builder.Configuration["APPBRIDGE_RDP_SIGNING_THUMBPRINT"]
    ?? throw new InvalidOperationException(
        "Set APPBRIDGE_RDP_SIGNING_THUMBPRINT before running the Control Plane (see docs/SETUP-DEV.md).");
var rdpSignerOptions = builder.Configuration["APPBRIDGE_RDPSIGN_PATH"] is { Length: > 0 } rdpSignExecutablePath
    ? new RdpSignerOptions { CertificateThumbprint = rdpSigningThumbprint, ExecutablePath = rdpSignExecutablePath }
    : new RdpSignerOptions { CertificateThumbprint = rdpSigningThumbprint };
builder.Services.AddSingleton<IRdpFileSigner>(new RdpSignExeSigner(rdpSignerOptions));

builder.Services.AddScoped<ISessionBackend, RdsSessionBackend>();
builder.Services.AddScoped<ISessionRegistry, SessionRegistry>();

// T-602: only the stale_expired defence (MODELO-DE-DADOS.md §6.2) — reconciled_missing needs a
// live Connection Broker query, which ADR-0006 already placed in MVP-1, not here.
builder.Services.AddSingleton(new SessionReconcilerOptions());
builder.Services.AddScoped<ISessionReconciler, SessionReconciler>();
builder.Services.AddHostedService<SessionReconciliationHostedService>();

// PD-04 (API.md §11), resolved by T-504: in-process memory, not a table or distributed cache — see
// IIdempotencyStore's own doc comment for why that's enough for MVP-0's single-instance topology.
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IIdempotencyStore, MemoryIdempotencyStore>();

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

        // T-505 (API.md §9): without this, a missing/expired/invalid bearer token on any
        // [Authorize]-gated route (GET /v1/applications, POST /v1/launches, ...) ends the pipeline
        // with a bare 401 and no body — the caller can't tell "token expired" from "never
        // authenticated" from any other reason. JwtBearerEvents runs before MVC/minimal API error
        // handling even looks at the request, so this, not GlobalExceptionHandler, is where
        // SESSION_EXPIRED has to be written.
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                var correlationId = context.HttpContext.Items.TryGetValue(CorrelationIdMiddleware.HeaderName, out var value)
                    && value is string resolvedCorrelationId
                        ? resolvedCorrelationId
                        : Guid.NewGuid().ToString();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(
                    GlobalProblems.SessionExpired(context.Request.Path, correlationId));
            },
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

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

// After CorrelationIdMiddleware (so GlobalExceptionHandler can read the id it set) and before
// everything else (so it wraps every exception downstream, including JSON body-binding failures
// inside endpoint invocation and anything Authentication/Authorization itself might throw).
app.UseExceptionHandler();

app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();

app.MapHealthChecks("/v1/health", new HealthCheckOptions
{
    ResponseWriter = HealthCheckResponseWriter.Write,
});

app.MapAuthEndpoints();
app.MapCatalogEndpoints();
app.MapLaunchEndpoints();

// RF-012: the catalog is populated by seed, not an admin panel (that's RF-043, MVP-1). A CLI verb
// instead of an HTTP route so this stays a seed, not the very panel RF-012 says the catalog does
// without — `dotnet run -- seed-catalog <ad-domain>` seeds CatalogSeeder's fixed dogfood dataset
// into the tenant identified by Tenant.AdDomain, then exits without starting the host.
if (args.Length > 0 && args[0] == "seed-catalog")
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("Uso: dotnet run -- seed-catalog <ad-domain-do-tenant>");
        return 1;
    }

    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppBridgeDbContext>();
    var tenant = await dbContext.Tenants.SingleOrDefaultAsync(t => t.AdDomain == args[1]);
    if (tenant is null)
    {
        Console.Error.WriteLine($"Tenant com ad_domain '{args[1]}' não encontrado.");
        return 1;
    }

    scope.ServiceProvider.GetRequiredService<TenantContext>().TenantId = tenant.Id;
    await CatalogSeeder.SeedAsync(dbContext, tenant.Id);
    Console.WriteLine($"Catálogo semeado para o tenant '{tenant.Name}' ({tenant.Id}).");
    return 0;
}

app.Run();
return 0;

public partial class Program
{
}
