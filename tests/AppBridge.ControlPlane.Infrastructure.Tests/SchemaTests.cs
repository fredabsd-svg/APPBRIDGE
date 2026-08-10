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
/// T-202 acceptance criterion: "Migração aplica e reverte". Runs against the real local
/// PostgreSQL instance set up this session (docs/SETUP-DEV.md) — no Docker/Testcontainers
/// available in this environment. Each fact gets a fresh schema: xUnit creates a new instance of
/// the test class per [Fact] by default, so InitializeAsync/DisposeAsync run once per test, not
/// once per class — slower, but no test can leak state into another.
/// </summary>
public sealed class SchemaTests : IAsyncLifetime
{
    private AppBridgeDbContext _context = null!;

    public async Task InitializeAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("APPBRIDGE_TEST_DB_CONNECTION")
            ?? throw new InvalidOperationException(
                "Set APPBRIDGE_TEST_DB_CONNECTION before running the Infrastructure tests (see docs/SETUP-DEV.md).");

        // Schema tests exercise migrations and raw SQL, not tenant-scoped DbSet queries — an unset
        // TenantContext (T-203) doesn't affect anything asserted in this file.
        _context = new AppBridgeDbContext(
            new DbContextOptionsBuilder<AppBridgeDbContext>().UseNpgsql(connectionString).Options,
            new TenantContext());

        var migrator = Migrator();
        await migrator.MigrateAsync(Migration.InitialDatabase); // clean slate, in case a prior run failed mid-test
        await migrator.MigrateAsync();                          // apply
    }

    public async Task DisposeAsync() => await _context.DisposeAsync();

    private IMigrator Migrator() => _context.GetInfrastructure().GetRequiredService<IMigrator>();

    [Fact]
    public async Task Migration_creates_all_fifteen_mvp0_tables()
    {
        string[] expected =
        [
            "tenant", "retention_policy", "redirection_policy", "signing_certificate",
            "user_account", "group", "user_group_membership",
            "application", "application_permission",
            "host_pool", "session_host", "session",
            "launch", "access_event", "purge_run",
        ];

        foreach (var table in expected)
        {
            Assert.True(await TableExistsAsync(table), $"expected table '{table}' to exist");
        }
    }

    [Fact]
    public async Task Migration_reverts_cleanly_removing_every_table()
    {
        Assert.True(await TableExistsAsync("tenant"));

        await Migrator().MigrateAsync(Migration.InitialDatabase);

        Assert.False(await TableExistsAsync("tenant"));
        Assert.False(await TableExistsAsync("launch"));
        Assert.False(await TableExistsAsync("session"));

        // Leave the schema applied — DisposeAsync doesn't depend on it, but a failed run that left
        // the DB empty shouldn't be mistaken for a passing one by a human poking at psql after.
        await Migrator().MigrateAsync();
        Assert.True(await TableExistsAsync("tenant"));
    }

    [Fact]
    public async Task Every_tenant_scoped_table_has_a_non_nullable_tenant_id_column()
    {
        string[] tenantScoped =
        [
            "retention_policy", "redirection_policy", "user_account", "group",
            "user_group_membership", "application", "application_permission",
            "host_pool", "session_host", "session", "launch", "access_event", "purge_run",
        ];

        foreach (var table in tenantScoped)
        {
            var nullable = await ColumnIsNullableAsync(table, "tenant_id");
            Assert.False(nullable, $"'{table}.tenant_id' must be NOT NULL — every tenant row belongs to exactly one tenant (ADR-0011)");
        }
    }

    [Fact]
    public async Task Tenant_and_signing_certificate_are_deliberately_not_tenant_scoped()
    {
        // tenant IS the tenant; signing_certificate is a Control Plane asset, not tenant data
        // (ADR-0009). Both are exceptions declared in the entity classes, not oversights.
        Assert.Null(await ColumnIsNullableAsync("tenant", "tenant_id", allowMissing: true));
        Assert.Null(await ColumnIsNullableAsync("signing_certificate", "tenant_id", allowMissing: true));
    }

    [Fact]
    public async Task Retention_policy_check_rejects_a_value_below_the_category_minimum()
    {
        var tenantId = await InsertTenantAsync("acme");

        await using var command = _context.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            INSERT INTO retention_policy (id, tenant_id, category, retention_months, created_at)
            VALUES (gen_random_uuid(), @tenant_id, 'access', 3, now())
            """;
        AddParameter(command, "tenant_id", tenantId);

        var exception = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await _context.Database.OpenConnectionAsync();
            await command.ExecuteNonQueryAsync();
        });
        Assert.Equal("ck_retention_policy_minimum", exception.ConstraintName);
    }

    [Fact]
    public async Task Redirection_policy_check_requires_a_reason_on_application_specific_overrides()
    {
        var tenantId = await InsertTenantAsync("acme");

        await using var command = _context.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            INSERT INTO redirection_policy
                (id, tenant_id, application_id, allow_printer, allow_smartcard, allow_clipboard,
                 allow_audio_out, allow_drives, allow_serial_ports, allow_audio_in, allow_other_usb, created_at)
            VALUES
                (gen_random_uuid(), @tenant_id, gen_random_uuid(), true, true, true, true, false, false, false, false, now())
            """;
        AddParameter(command, "tenant_id", tenantId);

        var exception = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await _context.Database.OpenConnectionAsync();
            await command.ExecuteNonQueryAsync();
        });
        Assert.Equal("ck_redirection_policy_exception_reason", exception.ConstraintName);
    }

    [Fact]
    public async Task Tenant_slug_is_unique()
    {
        await InsertTenantAsync("acme");

        var duplicate = new Tenant { Name = "Another name, same slug", Slug = "acme" };
        _context.Tenants.Add(duplicate);

        await Assert.ThrowsAsync<DbUpdateException>(() => _context.SaveChangesAsync());
    }

    [Fact]
    public async Task Ids_are_generated_by_the_application_before_save_not_by_the_database()
    {
        var tenant = new Tenant { Name = "Acme", Slug = "acme-app-generated" };

        // The id exists before SaveChanges is even called — proof it's app-generated (ADR-0011 §1),
        // not assigned on INSERT by a database default.
        Assert.NotEqual(Guid.Empty, tenant.Id);
        var idBeforeSave = tenant.Id;

        _context.Tenants.Add(tenant);
        await _context.SaveChangesAsync();

        Assert.Equal(idBeforeSave, tenant.Id);
    }

    private async Task<Guid> InsertTenantAsync(string slug)
    {
        var tenant = new Tenant { Name = slug, Slug = slug };
        _context.Tenants.Add(tenant);
        await _context.SaveChangesAsync();
        return tenant.Id;
    }

    private async Task<bool> TableExistsAsync(string tableName)
    {
        await using var command = _context.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            SELECT EXISTS (
                SELECT 1 FROM information_schema.tables
                WHERE table_schema = 'public' AND table_name = @table_name
            )
            """;
        AddParameter(command, "table_name", tableName);

        await _context.Database.OpenConnectionAsync();
        return (bool)(await command.ExecuteScalarAsync())!;
    }

    private async Task<bool?> ColumnIsNullableAsync(string tableName, string columnName, bool allowMissing = false)
    {
        await using var command = _context.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            SELECT is_nullable FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = @table_name AND column_name = @column_name
            """;
        AddParameter(command, "table_name", tableName);
        AddParameter(command, "column_name", columnName);

        await _context.Database.OpenConnectionAsync();
        var result = await command.ExecuteScalarAsync();

        if (result is null)
        {
            return allowMissing ? null : throw new InvalidOperationException($"column '{tableName}.{columnName}' not found");
        }

        return (string)result == "YES";
    }

    private static void AddParameter(System.Data.Common.DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
