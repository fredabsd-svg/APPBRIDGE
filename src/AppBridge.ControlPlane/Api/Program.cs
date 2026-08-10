using System.IdentityModel.Tokens.Jwt;
using System.Text;
using AppBridge.ControlPlane.Application.Abstractions.Authentication;
using AppBridge.ControlPlane.Application.Abstractions.Authorization;
using AppBridge.ControlPlane.Application.Abstractions.Auditing;
using AppBridge.ControlPlane.Application.Abstractions.Context;
using AppBridge.ControlPlane.Core.Configuration;
using AppBridge.ControlPlane.Infrastructure.Authorization;
using AppBridge.ControlPlane.Infrastructure.Middleware;
using AppBridge.ControlPlane.Infrastructure.Persistence;
using AppBridge.ControlPlane.Infrastructure.Services.Authentication;
using AppBridge.ControlPlane.Infrastructure.Services.Authorization;
using AppBridge.ControlPlane.Infrastructure.Services.Auditing;
using AppBridge.ControlPlane.Infrastructure.Services.Context;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Add configuration
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<AuthenticationOptions>(builder.Configuration.GetSection("Authentication"));
builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection("Database"));

// Add database
var databaseOptions = builder.Configuration.GetSection("Database").Get<DatabaseOptions>();
builder.Services.AddDbContext<AppBridgeDbContext>(options =>
    options.UseNpgsql(
        databaseOptions?.ConnectionString ?? throw new InvalidOperationException("Database connection string not configured"),
        npgsqlOptions => npgsqlOptions.CommandTimeout(databaseOptions.CommandTimeoutSeconds)));

// Add authentication services
builder.Services.AddScoped<ITokenService, JwtTokenService>();
builder.Services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();

// Add context services
builder.Services.AddScoped<ITenantContextService, TenantContextService>();

// Add auditing services
builder.Services.AddScoped<IAuditingService, AuditingService>();

// Add authorization services
builder.Services.AddScoped<IAuthorizationService, AuthorizationService>();
builder.Services.AddSingleton<IAuthorizationHandler, ApplicationAccessAuthorizationHandler>();

// Add authentication
var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>();
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions?.SigningKey ?? throw new InvalidOperationException("JWT signing key not configured")));

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = signingKey,
        ValidateIssuer = jwtOptions.Schemes.ValidateIssuer,
        ValidIssuer = jwtOptions.Issuer,
        ValidateAudience = jwtOptions.Schemes.ValidateAudience,
        ValidAudience = jwtOptions.Audience,
        ValidateLifetime = jwtOptions.Schemes.ValidateLifetime,
        ClockSkew = jwtOptions.Schemes.ClockSkew,
        NameClaimType = JwtRegisteredClaimNames.UniqueName,
        RoleClaimType = "role",
    };
});

// Add authorization policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ApplicationAccess", policy =>
        policy.Requirements.Add(new ApplicationAccessRequirement()));
});

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer",
                }
            },
            new string[] { }
        }
    });
});

var app = builder.Build();

// Configure middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandling();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseTenantContext();
app.UseAuthorization();
app.UseAuditing();
app.MapControllers();

app.Run();
