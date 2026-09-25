using AppBridge.ControlPlane.Middleware;
using AppBridge.ControlPlane.Authentication;
using AppBridge.ControlPlane.Data;
using AppBridge.ControlPlane.Domain.Enums;
using AppBridge.ControlPlane.Launching;
using AppBridge.ControlPlane.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);
builder.Services.AddHealthChecks();
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<AuditWriter>();
builder.Services.AddScoped<AuthorizationService>();
builder.Services.AddScoped<LaunchMeteringService>();
builder.Services.AddScoped<RedirectionPolicyResolver>();
builder.Services.AddScoped<LaunchService>();
builder.Services.AddScoped<ISessionBackend, RdsSessionBackend>();
builder.Services.AddScoped<IRdpFileSigner, RdpSignExeSigner>();
builder.Services.AddSingleton<RdpDescriptorBuilder>();
builder.Services.Configure<RdpSigningOptions>(builder.Configuration.GetSection("RdpSigning"));
builder.Services.Configure<RdsSessionOptions>(builder.Configuration.GetSection("RdsSession"));
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower)));
builder.Services.Configure<IdentityProviderOptions>(builder.Configuration.GetSection("IdentityProvider"));

var tokenSection = builder.Configuration.GetSection("ControlPlaneTokens");
var tokenIssuer = tokenSection["Issuer"] ?? "https://appbridge.local";
var tokenAudience = tokenSection["Audience"] ?? "appbridge-launcher";
var tokenLifetimeMinutes = tokenSection.GetValue("LifetimeMinutes", 30);
var encodedSigningKey = tokenSection["SigningKey"];
if (string.IsNullOrWhiteSpace(encodedSigningKey) && builder.Environment.IsDevelopment())
{
    encodedSigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
}

if (string.IsNullOrWhiteSpace(encodedSigningKey))
{
    throw new InvalidOperationException("Configure ControlPlaneTokens:SigningKey com uma chave Base64 de pelo menos 32 bytes.");
}

byte[] signingKeyBytes;
try
{
    signingKeyBytes = Convert.FromBase64String(encodedSigningKey);
}
catch (FormatException exception)
{
    throw new InvalidOperationException("ControlPlaneTokens:SigningKey deve estar em Base64.", exception);
}

if (signingKeyBytes.Length < 32 || tokenLifetimeMinutes is < 1 or > 1440)
{
    throw new InvalidOperationException("A chave de token exige pelo menos 32 bytes e a validade deve estar entre 1 e 1440 minutos.");
}

var tokenOptions = new ControlPlaneTokenOptions
{
    Issuer = tokenIssuer,
    Audience = tokenAudience,
    SigningKey = encodedSigningKey,
    LifetimeMinutes = tokenLifetimeMinutes
};
builder.Services.Configure<ControlPlaneTokenOptions>(options =>
{
    options.Issuer = tokenOptions.Issuer;
    options.Audience = tokenOptions.Audience;
    options.SigningKey = tokenOptions.SigningKey;
    options.LifetimeMinutes = tokenOptions.LifetimeMinutes;
});
builder.Services.AddScoped<IdentityTokenValidator>();
builder.Services.AddScoped<IIdentityTokenValidator, IdentityTokenValidator>();
builder.Services.AddScoped<ControlPlaneTokenIssuer>();
builder.Services.AddScoped<AuthenticationSessionService>();
builder.Services.AddHostedService<CatalogSeedHostedService>();
builder.Services.AddHostedService<IdempotencyResponsePruner>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async challenge =>
            {
                challenge.HandleResponse();
                var correlationId = Guid.TryParse(challenge.HttpContext.TraceIdentifier, out var parsed)
                    ? parsed
                    : Guid.CreateVersion7();
                await Problem(StatusCodes.Status401Unauthorized, "SESSION_EXPIRED",
                    "Sua sessão expirou. Entre novamente.", challenge.Request.Path, correlationId)
                    .ExecuteAsync(challenge.HttpContext);
            }
        };
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = tokenOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = tokenOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(signingKeyBytes),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "name",
            RoleClaimType = ClaimTypes.Role
        };
    });
builder.Services.AddAuthorization();
var connectionString = builder.Configuration.GetConnectionString("AppBridge")
    ?? "Host=localhost;Database=appbridge;Username=appbridge";
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

var app = builder.Build();

app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    var correlationId = context.TraceIdentifier;

    if (exception is not null)
    {
        app.Logger.LogError(
            exception,
            "Unhandled request exception for {Method} {Path} with correlationId {correlationId}",
            context.Request.Method,
            context.Request.Path,
            correlationId);
    }

    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    context.Response.ContentType = "application/problem+json";

    var problem = new ProblemDetails
    {
        Type = "urn:appbridge:problem:internal-error",
        Title = "Falha interna",
        Status = StatusCodes.Status500InternalServerError,
        Detail = "Não foi possível concluir a solicitação. Informe o código de correlação ao suporte.",
        Instance = context.Request.Path
    };
    problem.Extensions["correlationId"] = correlationId;
    problem.Extensions["appbridgeCode"] = "INTERNAL_ERROR";

    await context.Response.WriteAsJsonAsync(
        problem,
        options: null,
        contentType: "application/problem+json",
        cancellationToken: context.RequestAborted);
}));

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseAuthentication();
app.UseMiddleware<TenantContextMiddleware>();
app.UseAuthorization();
app.MapHealthChecks("/health");
app.MapPost("/v1/auth/session", async (
    AuthenticationSessionRequest request,
    AuthenticationSessionService sessions,
    HttpContext context,
    CancellationToken cancellationToken) =>
{
    var correlationId = Guid.TryParse(context.TraceIdentifier, out var parsedCorrelationId)
        ? parsedCorrelationId
        : Guid.CreateVersion7();
    try
    {
        var result = await sessions.ExchangeAsync(
            request,
            context.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
            correlationId,
            cancellationToken);

        if (result.ErrorCode is not null)
        {
            var status = result.StatusCode;
            return Problem(status, result.ErrorCode, AuthErrorTitle(result.ErrorCode), context.Request.Path, correlationId);
        }

        return Results.Json(result.Response, statusCode: StatusCodes.Status201Created);
    }
    catch (IdentityProviderUnavailableException)
    {
        return Problem(StatusCodes.Status503ServiceUnavailable, "IDENTITY_PROVIDER_UNAVAILABLE",
            "O serviço de identidade está temporariamente indisponível.", context.Request.Path, correlationId);
    }
    catch (AuditUnavailableException)
    {
        return Problem(StatusCodes.Status503ServiceUnavailable, "AUDIT_UNAVAILABLE",
            "Não foi possível registrar o acesso. Tente novamente mais tarde.", context.Request.Path, correlationId);
    }
}).AllowAnonymous();

app.MapGet("/v1/applications", async (
    ClaimsPrincipal principal,
    AuthorizationService authorization,
    CancellationToken cancellationToken) =>
{
    if (!Guid.TryParse(principal.FindFirstValue("sub"), out var userAccountId))
    {
        return Results.Unauthorized();
    }

    var applications = await authorization.GetApplicationsAsync(userAccountId, cancellationToken);
    return Results.Ok(new
    {
        items = applications.Select(application => new
        {
            id = application.Id,
            displayName = application.DisplayName,
            description = application.Description,
            iconUrl = $"/v1/applications/{application.Id:D}/icon",
            launchMode = application.LaunchMode == ApplicationLaunchMode.RemoteApp ? "remote_app" : "confined_desktop",
            protocolUri = $"appbridge://launch/{application.Id:D}",
            available = true
        }),
        nextCursor = (string?)null
    });
}).RequireAuthorization();

app.MapPost("/v1/launches", async (
    LaunchRequest request,
    HttpContext context,
    LaunchService launches,
    CancellationToken cancellationToken) =>
{
    var correlationId = Guid.TryParse(context.TraceIdentifier, out var parsedCorrelationId)
        ? parsedCorrelationId
        : Guid.CreateVersion7();
    if (!Guid.TryParse(context.Request.Headers["Idempotency-Key"], out var idempotencyKey)
        || idempotencyKey == Guid.Empty)
    {
        return Problem(StatusCodes.Status400BadRequest, "MALFORMED_REQUEST",
            "A chave de idempotência é obrigatória.", context.Request.Path, correlationId);
    }

    if (!Guid.TryParse(context.User.FindFirstValue("sub"), out var userAccountId))
    {
        return Results.Unauthorized();
    }

    try
    {
        var result = await launches.LaunchAsync(
            userAccountId,
            request,
            idempotencyKey,
            context.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
            correlationId,
            cancellationToken);

        if (result.ErrorCode is not null)
        {
            return Problem(result.StatusCode, result.ErrorCode, LaunchErrorTitle(result.ErrorCode), context.Request.Path, correlationId);
        }

        return Results.Json(result.Response, statusCode: result.StatusCode);
    }
    catch (AuditUnavailableException)
    {
        return Problem(StatusCodes.Status503ServiceUnavailable, "AUDIT_UNAVAILABLE",
            "Não foi possível registrar o lançamento. Tente novamente mais tarde.", context.Request.Path, correlationId);
    }
}).RequireAuthorization();

app.Run();

static IResult Problem(int status, string code, string title, string? instance, Guid correlationId)
{
    var details = new ProblemDetails
    {
        Type = $"urn:appbridge:problem:{code.ToLowerInvariant().Replace('_', '-')}",
        Title = title,
        Status = status,
        Instance = instance
    };
    details.Extensions["correlationId"] = correlationId;
    details.Extensions["appbridgeCode"] = code;
    return Results.Problem(details);
}

static string AuthErrorTitle(string code) => code switch
{
    "MALFORMED_REQUEST" => "A solicitação de entrada está incompleta ou inválida.",
    "SESSION_EXPIRED" => "Sua sessão expirou. Entre novamente.",
    "INVALID_IDENTITY_TOKEN" => "A sessão de identidade expirou. Entre novamente.",
    "USER_DISABLED" => "Esta conta está desabilitada. Procure o administrador.",
    "TENANT_SUSPENDED" => "O acesso deste escritório está suspenso. Procure o suporte.",
    "USER_NOT_PROVISIONED" => "Sua conta ainda não foi provisionada no AppBridge.",
    _ => "Não foi possível iniciar a sessão."
};

static string LaunchErrorTitle(string code) => code switch
{
    "PERMISSION_REVOKED" => "Seu acesso a este aplicativo foi removido.",
    "APPLICATION_NOT_FOUND" => "O aplicativo não está disponível no catálogo.",
    "APPLICATION_UNAVAILABLE" => "O aplicativo está temporariamente indisponível.",
    "SIGNING_UNAVAILABLE" => "Não foi possível preparar a conexão segura. Tente novamente mais tarde.",
    "IDEMPOTENCY_CONFLICT" => "A chave de solicitação já foi usada com outros dados.",
    "IDEMPOTENCY_KEY_EXPIRED" => "A solicitação expirou. Inicie um novo lançamento.",
    "IDEMPOTENCY_IN_PROGRESS" => "A solicitação ainda está sendo processada.",
    "USER_DISABLED" => "Esta conta está desabilitada. Procure o administrador.",
    "EXCEPTION_REASON_REQUIRED" => "A exceção de redirecionamento não tem justificativa.",
    _ => "Não foi possível iniciar o aplicativo."
};

public partial class Program { }
