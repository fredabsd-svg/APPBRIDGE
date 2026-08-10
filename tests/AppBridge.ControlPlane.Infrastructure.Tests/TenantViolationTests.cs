using AppBridge.ControlPlane.Domain.Catalog;
using AppBridge.ControlPlane.Domain.Identity;
using AppBridge.ControlPlane.Domain.Sessions;
using AppBridge.ControlPlane.Domain.Tenancy;
using AppBridge.ControlPlane.Domain.Trail;
using AppBridge.ControlPlane.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace AppBridge.ControlPlane.Infrastructure.Tests;

/// <summary>
/// T-206 — the suite's canonical home for SEGURANCA.md's <b>V-02</b> ("Teste automatizado de
/// violação de tenant: tentar ler e gravar dados de outro tenant e exigir falha", AM-07/AM-14,
/// ADR-0004 item 9). <see cref="TenantIsolationTests"/> (T-203) and
/// <see cref="TenantForeignKeyTests"/> (T-204) already prove the two mechanisms work — this file's
/// job is breadth: entity shapes those two didn't reach.
///
/// Two gaps closed here specifically:
/// <list type="bullet">
/// <item>Every read-isolation test so far used <c>Application</c>, which goes through
/// <c>SetTenantAndSoftDeleteFilter</c>. No test had exercised <c>SetTenantFilter</c> alone — the
/// path append-only trail entities take, since they have no <c>deleted_at</c> (ADR-0011 §3). This
/// file's <c>AccessEvent</c> case is the first to reach that code path.</item>
/// <item>The only write-side FK tested was a required, single-target composite FK
/// (<c>application → host_pool</c>). This file adds a <b>nullable</b> composite FK
/// (<c>redirection_policy.application_id</c>) and a table with <b>two independent FKs to the same
/// principal type</b> (<c>application_permission.granted_by</c>/<c>revoked_by</c>, both →
/// <c>user_account</c>) — the shape most likely to hide an EF Core configuration mistake, since a
/// copy-paste error could silently point both at the same shadow property.</item>
/// </list>
///
/// Not attempted: exhaustive 1:1 coverage of all 13 tenant-scoped tables and all 15 composite FKs.
/// ADR-0004 item 9 asks for "casos que tentam ler e escrever dado de outro tenant", not a
/// combinatorial matrix — the remaining relationships share one of the two mechanisms already
/// proven across five distinct entity/relationship shapes between this file and T-203/T-204.
/// </summary>
public sealed class TenantViolationTests : IAsyncLifetime
{
    private string _connectionString = null!;
    private Guid _tenantAId;
    private Guid _tenantBId;
    private Guid _applicationAId;
    private Guid _groupAId;
    private Guid _userAccountAId;
    private Guid _userAccountBId;

    public async Task InitializeAsync()
    {
        _connectionString = Environment.GetEnvironmentVariable("APPBRIDGE_TEST_DB_CONNECTION")
            ?? throw new InvalidOperationException(
                "Set APPBRIDGE_TEST_DB_CONNECTION before running the Infrastructure tests (see docs/SETUP-DEV.md).");

        await using var setup = NewContext(tenantId: null);
        var migrator = setup.GetInfrastructure().GetRequiredService<IMigrator>();
        await migrator.MigrateAsync(Migration.InitialDatabase); // clean slate
        await migrator.MigrateAsync();

        var tenantA = new Tenant { Name = "Escritório A", Slug = "escritorio-a" };
        var tenantB = new Tenant { Name = "Escritório B", Slug = "escritorio-b" };
        setup.Tenants.AddRange(tenantA, tenantB);
        await setup.SaveChangesAsync();
        _tenantAId = tenantA.Id;
        _tenantBId = tenantB.Id;

        var hostPoolA = new HostPool { TenantId = _tenantAId, Name = "Pool A" };
        setup.HostPools.Add(hostPoolA);
        await setup.SaveChangesAsync();

        var applicationA = new Application
        {
            TenantId = _tenantAId,
            DisplayName = "App A",
            RemoteAppAlias = "app-a",
            HostPoolId = hostPoolA.Id,
        };
        var groupA = new Group { TenantId = _tenantAId, Name = "Grupo A" };
        var userAccountA = new UserAccount
        {
            TenantId = _tenantAId,
            ExternalSubject = "oid-a",
            Upn = "a@escritorio-a.example",
            AdObjectSid = "S-1-5-21-0-0-0-1001",
        };
        var userAccountB = new UserAccount
        {
            TenantId = _tenantBId,
            ExternalSubject = "oid-b",
            Upn = "b@escritorio-b.example",
            AdObjectSid = "S-1-5-21-0-0-0-2001",
        };
        setup.Applications.Add(applicationA);
        setup.Groups.Add(groupA);
        setup.UserAccounts.AddRange(userAccountA, userAccountB);
        await setup.SaveChangesAsync();
        _applicationAId = applicationA.Id;
        _groupAId = groupA.Id;
        _userAccountAId = userAccountA.Id;
        _userAccountBId = userAccountB.Id;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private AppBridgeDbContext NewContext(Guid? tenantId) => new(
        new DbContextOptionsBuilder<AppBridgeDbContext>().UseNpgsql(_connectionString).Options,
        new TenantContext { TenantId = tenantId });

    // --- Read: gap not covered by TenantIsolationTests (T-203), which only exercised Application ---

    [Fact]
    public async Task Query_with_no_explicit_where_clause_returns_only_the_current_tenants_user_accounts()
    {
        await using var readAsTenantA = NewContext(_tenantAId);
        var visible = await readAsTenantA.UserAccounts.ToListAsync();

        var userAccount = Assert.Single(visible);
        Assert.Equal(_userAccountAId, userAccount.Id);
    }

    [Fact]
    public async Task Query_with_no_explicit_where_clause_returns_only_the_current_tenants_access_events()
    {
        await using (var asTenantA = NewContext(_tenantAId))
        {
            asTenantA.AccessEvents.Add(NewLoginEvent(_tenantAId));
            await asTenantA.SaveChangesAsync();
        }

        await using (var asTenantB = NewContext(_tenantBId))
        {
            asTenantB.AccessEvents.Add(NewLoginEvent(_tenantBId));
            await asTenantB.SaveChangesAsync();
        }

        // AccessEvent is append-only (no deleted_at, ADR-0011 §3) — this is the first test in the
        // suite to exercise AppBridgeDbContext.SetTenantFilter rather than
        // SetTenantAndSoftDeleteFilter.
        await using var readAsTenantA = NewContext(_tenantAId);
        var visible = await readAsTenantA.AccessEvents.ToListAsync();

        var accessEvent = Assert.Single(visible);
        Assert.Equal(_tenantAId, accessEvent.TenantId);
    }

    private static AccessEvent NewLoginEvent(Guid tenantId) => new()
    {
        TenantId = tenantId,
        EventType = "login",
        Result = AccessEventResult.Success,
        OccurredAt = DateTimeOffset.UtcNow,
        CorrelationId = Guid.NewGuid(),
    };

    // --- Write: gaps not covered by TenantForeignKeyTests (T-204), which only exercised the
    // required, single-target application -> host_pool relationship ---

    [Fact]
    public async Task Redirection_policy_override_referencing_another_tenants_application_is_rejected_by_the_database()
    {
        await using var context = NewContext(tenantId: null);
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            INSERT INTO redirection_policy
                (id, tenant_id, application_id, allow_printer, allow_smartcard, allow_clipboard,
                 allow_audio_out, allow_drives, allow_serial_ports, allow_audio_in, allow_other_usb,
                 exception_reason, created_at)
            VALUES
                (gen_random_uuid(), @tenant_id, @application_id, true, true, true, true, false, false, false, false,
                 'exceção de teste', now())
            """;
        AddParameter(command, "tenant_id", _tenantBId);       // tenant B ...
        AddParameter(command, "application_id", _applicationAId); // ... overriding tenant A's application

        var exception = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await context.Database.OpenConnectionAsync();
            await command.ExecuteNonQueryAsync();
        });
        Assert.Equal("fk_redirection_policy_application", exception.ConstraintName);
    }

    [Fact]
    public async Task Permission_granted_by_another_tenants_user_is_rejected_by_the_database()
    {
        // application_permission has TWO independent composite FKs to user_account (granted_by,
        // revoked_by) — the shape most likely to hide a copy-paste mistake in the EF Core
        // configuration (e.g. both accidentally wired to the same shadow property).
        await using var context = NewContext(tenantId: null);
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            INSERT INTO application_permission
                (id, tenant_id, application_id, group_id, effective_from, granted_by, created_at)
            VALUES
                (gen_random_uuid(), @tenant_id, @application_id, @group_id, now(), @granted_by, now())
            """;
        AddParameter(command, "tenant_id", _tenantAId);
        AddParameter(command, "application_id", _applicationAId);
        AddParameter(command, "group_id", _groupAId);
        AddParameter(command, "granted_by", _userAccountBId); // tenant B's user granting tenant A's permission

        var exception = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await context.Database.OpenConnectionAsync();
            await command.ExecuteNonQueryAsync();
        });
        Assert.Equal("fk_permission_granted_by", exception.ConstraintName);
    }

    private static void AddParameter(System.Data.Common.DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
