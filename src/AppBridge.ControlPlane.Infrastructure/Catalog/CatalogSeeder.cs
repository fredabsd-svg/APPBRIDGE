using AppBridge.ControlPlane.Domain.Catalog;
using AppBridge.ControlPlane.Domain.Sessions;
using Microsoft.EntityFrameworkCore;

namespace AppBridge.ControlPlane.Infrastructure.Catalog;

/// <summary>
/// RF-012: MVP-0's catalog is populated by seed (JSON or table), not an admin panel — the panel
/// itself (RF-043, "substituindo o seed de RF-012") is MVP-1, a later epic that hasn't started.
/// This is that seed: the fixed dogfood dataset VISAO.md §1/PA-01 names as the target
/// (Domínio Contábil, Alterdata), written straight to the <c>application</c>/<c>host_pool</c>
/// tables via EF Core — no separate JSON file, since nothing downstream reads one and RF-012 treats
/// "JSON ou tabela" as either being an acceptable seed mechanism, not a mandate for both.
///
/// Idempotent by design: safe to run more than once against the same tenant (dev restarts, a
/// re-run after a schema change) without duplicating rows or throwing on the unique index
/// (<c>uq_application_tenant_alias_pool</c>, MODELO-DE-DADOS.md §5.1).
/// </summary>
public static class CatalogSeeder
{
    private const string DefaultHostPoolName = "Pool Principal";

    private static readonly IReadOnlyList<SeedApplication> DogfoodApplications =
    [
        new("Domínio Contábil", "Escrita fiscal e contabilidade", "dominio-contabil"),
        new("Alterdata", "Sistema contábil e fiscal", "alterdata"),
    ];

    /// <summary>
    /// Caller is responsible for having set <c>TenantContext.TenantId</c> to
    /// <paramref name="tenantId"/> on <paramref name="dbContext"/>'s scope before calling — the
    /// existence checks below rely on the tenant-scoped <c>DbSet</c> filter (ADR-0004) to see only
    /// that tenant's rows, the same precondition every other tenant-scoped write in this codebase
    /// already carries.
    /// </summary>
    public static async Task SeedAsync(AppBridgeDbContext dbContext, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var hostPool = await dbContext.HostPools.SingleOrDefaultAsync(h => h.Name == DefaultHostPoolName, cancellationToken);
        if (hostPool is null)
        {
            hostPool = new HostPool { TenantId = tenantId, Name = DefaultHostPoolName };
            dbContext.HostPools.Add(hostPool);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        foreach (var seed in DogfoodApplications)
        {
            var alreadySeeded = await dbContext.Applications
                .AnyAsync(a => a.RemoteAppAlias == seed.RemoteAppAlias && a.HostPoolId == hostPool.Id, cancellationToken);
            if (alreadySeeded)
            {
                continue;
            }

            dbContext.Applications.Add(new Application
            {
                TenantId = tenantId,
                DisplayName = seed.DisplayName,
                Description = seed.Description,
                RemoteAppAlias = seed.RemoteAppAlias,
                HostPoolId = hostPool.Id,
                Status = ApplicationStatus.Published,
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private sealed record SeedApplication(string DisplayName, string Description, string RemoteAppAlias);
}
