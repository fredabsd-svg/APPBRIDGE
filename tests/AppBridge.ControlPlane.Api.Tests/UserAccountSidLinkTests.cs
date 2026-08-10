using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AppBridge.ControlPlane.Api.Endpoints;
using AppBridge.ControlPlane.Domain.Identity;
using AppBridge.ControlPlane.Domain.Tenancy;
using AppBridge.ControlPlane.Infrastructure;
using AppBridge.ControlPlane.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AppBridge.ControlPlane.Api.Tests;

/// <summary>
/// T-302 acceptance criterion: "Renomear a conta no AD não quebra o vínculo nem a trilha (RF-002)".
/// <see cref="UserAccount.AdObjectSid"/> is what MODELO-DE-DADOS.md §4.1 documents as the field a
/// rename never touches — <c>Upn</c>/<c>DisplayName</c> are what it actually changes. These tests
/// simulate a rename between two logins of the same identity (the dev provider's extended token
/// form carries a fresh Upn/DisplayName, standing in for a fresh directory read) and prove the
/// login endpoint refreshes those fields **in place**, on the same row, rather than losing or
/// duplicating the link.
/// </summary>
public sealed class UserAccountSidLinkTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };

    private string _connectionString = null!;
    private ApiTestFactory _factory = null!;
    private HttpClient _client = null!;
    private Guid _tenantId;
    private Guid _userAccountId;

    public async Task InitializeAsync()
    {
        _connectionString = Environment.GetEnvironmentVariable("APPBRIDGE_TEST_DB_CONNECTION")
            ?? throw new InvalidOperationException(
                "Set APPBRIDGE_TEST_DB_CONNECTION before running the Api tests (see docs/SETUP-DEV.md).");

        _factory = new ApiTestFactory();
        _client = _factory.CreateClient();

        await using var context = NewDbContext();
        var migrator = context.GetInfrastructure().GetRequiredService<IMigrator>();
        await migrator.MigrateAsync(Migration.InitialDatabase); // clean slate
        await migrator.MigrateAsync();

        var tenant = new Tenant { Name = "Escritório A", Slug = "escritorio-a", AdDomain = "escritorio-a.local" };
        context.Tenants.Add(tenant);
        await context.SaveChangesAsync();
        _tenantId = tenant.Id;

        var user = new UserAccount
        {
            TenantId = _tenantId,
            ExternalSubject = "oid-ana",
            Upn = "ana.souza@escritorio-a.local",
            AdObjectSid = "S-1-5-21-0-0-0-1001",
            DisplayName = "Ana Souza",
        };
        context.UserAccounts.Add(user);
        await context.SaveChangesAsync();
        _userAccountId = user.Id;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    private AppBridgeDbContext NewDbContext() => new(
        new DbContextOptionsBuilder<AppBridgeDbContext>().UseNpgsql(_connectionString).Options,
        new TenantContext { TenantId = _tenantId });

    [Fact]
    public async Task Renaming_the_AD_account_updates_Upn_and_DisplayName_on_the_same_row_and_keeps_the_trail_intact()
    {
        var firstLogin = await _client.PostAsJsonAsync(
            "/v1/auth/session", new { identityToken = "dev:oid-ana:escritorio-a.local" });
        Assert.Equal(HttpStatusCode.Created, firstLogin.StatusCode);

        // Rename in AD: same identity (ExternalSubject/AdDomain unchanged, same SID behind the
        // scenes), but the directory now reports a new UPN and display name.
        var secondLogin = await _client.PostAsJsonAsync(
            "/v1/auth/session",
            new
            {
                identityToken = "dev:oid-ana:escritorio-a.local:ana.pereira@escritorio-a.local:Ana Pereira",
            });

        Assert.Equal(HttpStatusCode.Created, secondLogin.StatusCode);
        var body = await secondLogin.Content.ReadFromJsonAsync<SessionResponse>(ReadOptions);
        Assert.Equal("Ana Pereira", body!.User.DisplayName);

        await using var verify = NewDbContext();

        // Still exactly one account — the rename did not provision a second row.
        var accounts = await verify.UserAccounts.Where(u => u.ExternalSubject == "oid-ana").ToListAsync();
        var account = Assert.Single(accounts);
        Assert.Equal(_userAccountId, account.Id);

        // The vínculo's anchor never moves; only the fields AD actually changed on rename do.
        Assert.Equal("S-1-5-21-0-0-0-1001", account.AdObjectSid);
        Assert.Equal("ana.pereira@escritorio-a.local", account.Upn);
        Assert.Equal("Ana Pereira", account.DisplayName);

        // The trail: both logins — before and after the rename — reference the same account.
        var loginEvents = await verify.AccessEvents
            .Where(e => e.EventType == "login" && e.UserAccountId == _userAccountId)
            .ToListAsync();
        Assert.Equal(2, loginEvents.Count);
    }

    [Fact]
    public async Task A_login_without_directory_attributes_in_the_token_leaves_Upn_and_DisplayName_unchanged()
    {
        var response = await _client.PostAsJsonAsync(
            "/v1/auth/session", new { identityToken = "dev:oid-ana:escritorio-a.local" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        await using var verify = NewDbContext();
        var account = await verify.UserAccounts.SingleAsync(u => u.Id == _userAccountId);
        Assert.Equal("ana.souza@escritorio-a.local", account.Upn);
        Assert.Equal("Ana Souza", account.DisplayName);
    }
}
