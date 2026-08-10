using AppBridge.ControlPlane.Domain.Catalog;
using AppBridge.ControlPlane.Domain.Tenancy;
using AppBridge.ControlPlane.Infrastructure.Catalog;
using AppBridge.ControlPlane.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AppBridge.ControlPlane.Infrastructure.Tests;

/// <summary>
/// T-401 acceptance criterion: "Catálogo carregado sem painel (RF-012)". <see cref="CatalogSeeder"/>
/// is the seed mechanism — these tests prove it populates the fixed dogfood dataset (VISAO.md §1/
/// PA-01: Domínio Contábil, Alterdata) and that it's safe to run more than once against the same
/// tenant without duplicating rows, since a dev restart or a re-seed after a schema change is
/// expected to call it again.
/// </summary>
public sealed class CatalogSeederTests : IAsyncLifetime
{
    private string _connectionString = null!;
    private Guid _tenantId;

    public async Task InitializeAsync()
    {
        _connectionString = Environment.GetEnvironmentVariable("APPBRIDGE_TEST_DB_CONNECTION")
            ?? throw new InvalidOperationException(
                "Set APPBRIDGE_TEST_DB_CONNECTION before running the Infrastructure tests (see docs/SETUP-DEV.md).");

        await using var setup = NewContext();
        var migrator = setup.GetInfrastructure().GetRequiredService<IMigrator>();
        await migrator.MigrateAsync(Migration.InitialDatabase); // clean slate
        await migrator.MigrateAsync();

        var tenant = new Tenant { Name = "Escritório A", Slug = "escritorio-a" };
        setup.Tenants.Add(tenant);
        await setup.SaveChangesAsync();
        _tenantId = tenant.Id;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private AppBridgeDbContext NewContext() => new(
        new DbContextOptionsBuilder<AppBridgeDbContext>().UseNpgsql(_connectionString).Options,
        new TenantContext { TenantId = _tenantId });

    [Fact]
    public async Task SeedAsync_populates_the_dogfood_applications_published_and_ready_to_serve()
    {
        await using (var context = NewContext())
        {
            await CatalogSeeder.SeedAsync(context, _tenantId);
        }

        await using var verify = NewContext();
        var applications = await verify.Applications.OrderBy(a => a.DisplayName).ToListAsync();

        Assert.Equal(2, applications.Count);
        Assert.Equal(["Alterdata", "Domínio Contábil"], applications.Select(a => a.DisplayName));
        Assert.All(applications, a => Assert.Equal(ApplicationStatus.Published, a.Status));
        Assert.All(applications, a => Assert.NotEqual(Guid.Empty, a.HostPoolId));

        var hostPools = await verify.HostPools.ToListAsync();
        Assert.Single(hostPools); // both applications share the one seeded pool
    }

    [Fact]
    public async Task SeedAsync_run_twice_does_not_duplicate_applications_or_host_pools()
    {
        await using (var first = NewContext())
        {
            await CatalogSeeder.SeedAsync(first, _tenantId);
        }

        await using (var second = NewContext())
        {
            await CatalogSeeder.SeedAsync(second, _tenantId);
        }

        await using var verify = NewContext();
        Assert.Equal(2, await verify.Applications.CountAsync());
        Assert.Equal(1, await verify.HostPools.CountAsync());
    }
}
