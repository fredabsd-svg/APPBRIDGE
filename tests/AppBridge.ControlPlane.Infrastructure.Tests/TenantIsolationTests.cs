using AppBridge.ControlPlane.Domain.Catalog;
using AppBridge.ControlPlane.Domain.Tenancy;
using AppBridge.ControlPlane.Infrastructure;
using AppBridge.ControlPlane.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AppBridge.ControlPlane.Infrastructure.Tests;

/// <summary>
/// T-203 acceptance criterion: "Consulta sem cláusula explícita não retorna dado de outro tenant"
/// (ADR-0004). Every assertion here queries a bare <c>DbSet</c> with no <c>Where</c> clause of its
/// own — the filter, not the test, is what's under test. Same real-PostgreSQL approach as
/// <see cref="SchemaTests"/> (no Docker/Testcontainers in this environment, see docs/SETUP-DEV.md).
/// </summary>
public sealed class TenantIsolationTests : IAsyncLifetime
{
    private string _connectionString = null!;
    private Guid _tenantAId;
    private Guid _tenantBId;

    public async Task InitializeAsync()
    {
        _connectionString = Environment.GetEnvironmentVariable("APPBRIDGE_TEST_DB_CONNECTION")
            ?? throw new InvalidOperationException(
                "Set APPBRIDGE_TEST_DB_CONNECTION before running the Infrastructure tests (see docs/SETUP-DEV.md).");

        await using var migrationContext = NewContext(tenantId: null);
        var migrator = migrationContext.GetInfrastructure().GetRequiredService<IMigrator>();
        await migrator.MigrateAsync(Migration.InitialDatabase); // clean slate
        await migrator.MigrateAsync();

        // Tenant itself isn't tenant-scoped, so any context can create both — mirrors how a real
        // provisioning flow (outside any single tenant's session) would do it.
        await using var setup = NewContext(tenantId: null);
        var tenantA = new Tenant { Name = "Escritório A", Slug = "escritorio-a" };
        var tenantB = new Tenant { Name = "Escritório B", Slug = "escritorio-b" };
        setup.Tenants.AddRange(tenantA, tenantB);
        await setup.SaveChangesAsync();
        _tenantAId = tenantA.Id;
        _tenantBId = tenantB.Id;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private AppBridgeDbContext NewContext(Guid? tenantId) => new(
        new DbContextOptionsBuilder<AppBridgeDbContext>().UseNpgsql(_connectionString).Options,
        new TenantContext { TenantId = tenantId });

    [Fact]
    public async Task Query_with_no_explicit_where_clause_returns_only_the_current_tenants_rows()
    {
        await using (var asTenantA = NewContext(_tenantAId))
        {
            asTenantA.Applications.Add(NewApplication(_tenantAId, "App da A"));
            await asTenantA.SaveChangesAsync();
        }

        await using (var asTenantB = NewContext(_tenantBId))
        {
            asTenantB.Applications.Add(NewApplication(_tenantBId, "App da B"));
            await asTenantB.SaveChangesAsync();
        }

        await using var readAsTenantA = NewContext(_tenantAId);
        var visible = await readAsTenantA.Applications.ToListAsync(); // no .Where(...) — the filter carries the whole burden

        var application = Assert.Single(visible);
        Assert.Equal("App da A", application.DisplayName);
        Assert.Equal(_tenantAId, application.TenantId);
    }

    [Fact]
    public async Task Query_with_no_tenant_resolved_returns_nothing_not_every_tenants_rows()
    {
        await using (var asTenantA = NewContext(_tenantAId))
        {
            asTenantA.Applications.Add(NewApplication(_tenantAId, "App da A"));
            await asTenantA.SaveChangesAsync();
        }

        await using var withoutTenant = NewContext(tenantId: null);
        var visible = await withoutTenant.Applications.ToListAsync();

        Assert.Empty(visible); // fail closed, not fail open
    }

    [Fact]
    public async Task Soft_deleted_row_is_excluded_by_the_default_query()
    {
        Guid applicationId;
        await using (var asTenantA = NewContext(_tenantAId))
        {
            var application = NewApplication(_tenantAId, "App descontinuada");
            asTenantA.Applications.Add(application);
            await asTenantA.SaveChangesAsync();
            applicationId = application.Id;

            application.DeletedAt = DateTimeOffset.UtcNow;
            application.DeletedBy = Guid.CreateVersion7();
            await asTenantA.SaveChangesAsync();
        }

        await using var readAsTenantA = NewContext(_tenantAId);
        var visible = await readAsTenantA.Applications.ToListAsync();
        Assert.Empty(visible);

        var includingDeleted = await readAsTenantA.Applications.IgnoreQueryFilters()
            .SingleAsync(a => a.Id == applicationId);
        Assert.NotNull(includingDeleted.DeletedAt);
    }

    [Fact]
    public async Task IgnoreQueryFilters_is_the_explicit_escape_hatch_for_deliberate_tenant_crossing()
    {
        await using (var asTenantA = NewContext(_tenantAId))
        {
            asTenantA.Applications.Add(NewApplication(_tenantAId, "App da A"));
            await asTenantA.SaveChangesAsync();
        }

        await using (var asTenantB = NewContext(_tenantBId))
        {
            asTenantB.Applications.Add(NewApplication(_tenantBId, "App da B"));
            await asTenantB.SaveChangesAsync();
        }

        // ADR-0004 item 7: crossing tenants is legitimate only when explicit in code. This proves
        // the filter is a default, not a wall — RF-075's provider role (MVP-1) will call exactly
        // this, nominally and audited, not invent its own bypass.
        await using var readAsTenantA = NewContext(_tenantAId);
        var everyone = await readAsTenantA.Applications.IgnoreQueryFilters().ToListAsync();

        Assert.Equal(2, everyone.Count);
    }

    private static Application NewApplication(Guid tenantId, string displayName) => new()
    {
        TenantId = tenantId,
        DisplayName = displayName,
        RemoteAppAlias = Guid.NewGuid().ToString("N"),
        HostPoolId = Guid.CreateVersion7(), // no FK enforced yet (T-204) — a random id is a valid stand-in here
    };
}
