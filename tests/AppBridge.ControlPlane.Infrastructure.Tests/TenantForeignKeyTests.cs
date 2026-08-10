using AppBridge.ControlPlane.Domain.Sessions;
using AppBridge.ControlPlane.Domain.Tenancy;
using AppBridge.ControlPlane.Infrastructure;
using AppBridge.ControlPlane.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace AppBridge.ControlPlane.Infrastructure.Tests;

/// <summary>
/// T-204 acceptance criterion: "Tentativa de gravar referência cruzada é recusada pelo banco"
/// (ADR-0011 §4). The read-side filter (T-203) never runs for this — a raw INSERT proves the
/// database itself enforces it, independent of the application layer. <c>application → host_pool</c>
/// is exactly the relationship pair the ADR uses as its worked example.
/// </summary>
public sealed class TenantForeignKeyTests : IAsyncLifetime
{
    private AppBridgeDbContext _context = null!;
    private Guid _tenantAId;
    private Guid _tenantBId;
    private Guid _hostPoolAId;

    public async Task InitializeAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("APPBRIDGE_TEST_DB_CONNECTION")
            ?? throw new InvalidOperationException(
                "Set APPBRIDGE_TEST_DB_CONNECTION before running the Infrastructure tests (see docs/SETUP-DEV.md).");

        _context = new AppBridgeDbContext(
            new DbContextOptionsBuilder<AppBridgeDbContext>().UseNpgsql(connectionString).Options,
            new TenantContext());

        var migrator = _context.GetInfrastructure().GetRequiredService<IMigrator>();
        await migrator.MigrateAsync(Migration.InitialDatabase); // clean slate
        await migrator.MigrateAsync();

        var tenantA = new Tenant { Name = "Tenant A", Slug = "tenant-a" };
        var tenantB = new Tenant { Name = "Tenant B", Slug = "tenant-b" };
        _context.Tenants.AddRange(tenantA, tenantB);
        await _context.SaveChangesAsync();
        _tenantAId = tenantA.Id;
        _tenantBId = tenantB.Id;

        var hostPoolA = new HostPool { TenantId = _tenantAId, Name = "Pool A" };
        _context.HostPools.Add(hostPoolA);
        await _context.SaveChangesAsync();
        _hostPoolAId = hostPoolA.Id;
    }

    public async Task DisposeAsync() => await _context.DisposeAsync();

    [Fact]
    public async Task Application_referencing_another_tenants_host_pool_is_rejected_by_the_database()
    {
        await using var command = _context.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            INSERT INTO application (id, tenant_id, display_name, remote_app_alias, host_pool_id, launch_mode, status, created_at)
            VALUES (gen_random_uuid(), @tenant_id, 'App cruzado', 'app-cruzado', @host_pool_id, 'remote_app', 'draft', now())
            """;
        AddParameter(command, "tenant_id", _tenantBId);   // tenant B ...
        AddParameter(command, "host_pool_id", _hostPoolAId); // ... pointing at tenant A's pool

        var exception = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await _context.Database.OpenConnectionAsync();
            await command.ExecuteNonQueryAsync();
        });
        Assert.Equal("fk_application_host_pool", exception.ConstraintName);
    }

    [Fact]
    public async Task Application_referencing_its_own_tenants_host_pool_is_accepted()
    {
        await using var command = _context.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            INSERT INTO application (id, tenant_id, display_name, remote_app_alias, host_pool_id, launch_mode, status, created_at)
            VALUES (gen_random_uuid(), @tenant_id, 'App legítimo', 'app-legitimo', @host_pool_id, 'remote_app', 'draft', now())
            """;
        AddParameter(command, "tenant_id", _tenantAId);
        AddParameter(command, "host_pool_id", _hostPoolAId);

        await _context.Database.OpenConnectionAsync();
        var rowsInserted = await command.ExecuteNonQueryAsync();

        Assert.Equal(1, rowsInserted);
    }

    private static void AddParameter(System.Data.Common.DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
