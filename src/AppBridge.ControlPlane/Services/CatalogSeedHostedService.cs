using System.Text.Json;
using System.Text.Json.Serialization;
using AppBridge.ControlPlane.Data;
using AppBridge.ControlPlane.Domain.Entities;
using AppBridge.ControlPlane.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AppBridge.ControlPlane.Services;

public sealed class CatalogSeedHostedService(
    IServiceScopeFactory scopeFactory,
    IWebHostEnvironment environment,
    IConfiguration configuration,
    ILogger<CatalogSeedHostedService> logger) : IHostedService
{
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
    };

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var configuredPath = configuration["CatalogSeed:Path"] ?? "data/catalog.seed.json";
        var seedPath = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(environment.ContentRootPath, configuredPath);
        if (!File.Exists(seedPath))
        {
            logger.LogInformation("Arquivo de seed do catálogo não encontrado; nenhum aplicativo foi semeado.");
            return;
        }

        CatalogSeedDocument seed;
        try
        {
            await using var stream = File.OpenRead(seedPath);
            seed = await JsonSerializer.DeserializeAsync<CatalogSeedDocument>(stream, _jsonOptions, cancellationToken)
                ?? throw new InvalidDataException("O documento de seed está vazio.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("O arquivo JSON de seed do catálogo não é válido.", exception);
        }

        foreach (var tenantSeed in seed.Tenants)
        {
            if (tenantSeed.TenantId == Guid.Empty)
            {
                throw new InvalidDataException("Cada seed de catálogo precisa de um tenantId válido.");
            }

            await using var scope = scopeFactory.CreateAsyncScope();
            var tenantContext = scope.ServiceProvider.GetRequiredService<TenantContext>();
            tenantContext.Bind(tenantSeed.TenantId);
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (await dbContext.Tenants.SingleOrDefaultAsync(cancellationToken) is null)
            {
                throw new InvalidDataException($"O tenant {tenantSeed.TenantId:D} do seed não existe no banco.");
            }

            foreach (var appSeed in tenantSeed.Applications)
            {
                if (appSeed.Id == Guid.Empty || appSeed.HostPoolId == Guid.Empty
                    || string.IsNullOrWhiteSpace(appSeed.DisplayName)
                    || string.IsNullOrWhiteSpace(appSeed.RemoteAppAlias))
                {
                    throw new InvalidDataException("Uma aplicação do seed não tem os campos obrigatórios.");
                }

                var application = await dbContext.Applications.SingleOrDefaultAsync(
                    candidate => candidate.Id == appSeed.Id,
                    cancellationToken);
                if (application is null)
                {
                    application = new RemoteApplication
                    {
                        Id = appSeed.Id,
                        TenantId = tenantSeed.TenantId,
                        CreatedBy = Guid.Empty
                    };
                    dbContext.Applications.Add(application);
                }

                application.DisplayName = appSeed.DisplayName.Trim();
                application.Description = appSeed.Description ?? string.Empty;
                application.IconRef = appSeed.IconRef ?? string.Empty;
                application.RemoteAppAlias = appSeed.RemoteAppAlias.Trim();
                application.HostPoolId = appSeed.HostPoolId;
                application.LaunchMode = appSeed.LaunchMode;
                application.Status = appSeed.Status;
                application.ConcurrentLimit = appSeed.ConcurrentLimit;
                application.LicenseNotes = appSeed.LicenseNotes;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "Seed do catálogo aplicado ao tenant {tenantId} com {applicationCount} aplicativos.",
                tenantSeed.TenantId,
                tenantSeed.Applications.Count);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class CatalogSeedDocument
{
    public List<CatalogTenantSeed> Tenants { get; init; } = [];
}

public sealed class CatalogTenantSeed
{
    public Guid TenantId { get; init; }
    public List<CatalogApplicationSeed> Applications { get; init; } = [];
}

public sealed class CatalogApplicationSeed
{
    public Guid Id { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? IconRef { get; init; }
    public string RemoteAppAlias { get; init; } = string.Empty;
    public Guid HostPoolId { get; init; }
    public ApplicationLaunchMode LaunchMode { get; init; }
    public ApplicationStatus Status { get; init; }
    public int? ConcurrentLimit { get; init; }
    public string? LicenseNotes { get; init; }
}
