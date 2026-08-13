using AppBridge.ControlPlane.Domain.Catalog;
using AppBridge.ControlPlane.Domain.Sessions;
using AppBridge.ControlPlane.Domain.Tenancy;
using AppBridge.ControlPlane.Infrastructure.Sessions;
using AppBridge.ControlPlane.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AppBridge.ControlPlane.Infrastructure.Tests;

/// <summary>
/// T-503 acceptance criterion: "Nenhuma regra de negócio referencia tipo do RDS (RNF-035)" —
/// verified structurally (<see cref="RdsSessionBackend"/> is the only thing in this codebase that
/// knows "RDS" exists) plus the behavior itself: host resolution picks an online host from the
/// right pool, and the descriptor it builds is exactly what T-501's <c>IRdpDescriptorBuilder</c>
/// consumes. Real PostgreSQL, no fake needed — unlike T-502's <c>rdpsign.exe</c>, this scope
/// ("resolução de host e descritor") never talks to a real RD Connection Broker; it reads
/// <c>SessionHost</c> rows this codebase already owns.
/// </summary>
public sealed class RdsSessionBackendTests : IAsyncLifetime
{
    private string _connectionString = null!;
    private Guid _tenantId;
    private Guid _hostPoolId;
    private Guid _applicationId;

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

        var hostPool = new HostPool { TenantId = _tenantId, Name = "Pool A" };
        setup.HostPools.Add(hostPool);
        await setup.SaveChangesAsync();
        _hostPoolId = hostPool.Id;

        var application = new Application
        {
            TenantId = _tenantId,
            DisplayName = "Domínio Contábil",
            RemoteAppAlias = "dominio-contabil",
            HostPoolId = hostPool.Id,
            Status = ApplicationStatus.Published,
        };
        setup.Applications.Add(application);
        await setup.SaveChangesAsync();
        _applicationId = application.Id;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private AppBridgeDbContext NewContext() => new(
        new DbContextOptionsBuilder<AppBridgeDbContext>().UseNpgsql(_connectionString).Options,
        new TenantContext { TenantId = _tenantId });

    private SessionHost NewHost(string fqdn, SessionHostStatus status) => new()
    {
        TenantId = _tenantId,
        HostPoolId = _hostPoolId,
        Fqdn = fqdn,
        Status = status,
    };

    [Fact]
    public async Task Resolves_an_online_host_from_the_applications_pool()
    {
        await using (var context = NewContext())
        {
            context.SessionHosts.Add(NewHost("ab-rds01.escritorio-a.local", SessionHostStatus.Online));
            await context.SaveChangesAsync();
        }

        await using var verify = NewContext();
        var host = await new RdsSessionBackend(verify).ResolveHostAsync(_applicationId);

        Assert.NotNull(host);
        Assert.Equal("ab-rds01.escritorio-a.local", host!.Fqdn);
    }

    [Fact]
    public async Task Excludes_draining_and_offline_hosts()
    {
        await using (var context = NewContext())
        {
            context.SessionHosts.AddRange(
                NewHost("draining.escritorio-a.local", SessionHostStatus.Draining),
                NewHost("offline.escritorio-a.local", SessionHostStatus.Offline));
            await context.SaveChangesAsync();
        }

        await using var verify = NewContext();
        var host = await new RdsSessionBackend(verify).ResolveHostAsync(_applicationId);

        Assert.Null(host);
    }

    [Fact]
    public async Task Picks_the_earliest_created_online_host_deterministically()
    {
        await using (var context = NewContext())
        {
            var first = NewHost("first.escritorio-a.local", SessionHostStatus.Online);
            context.SessionHosts.Add(first);
            await context.SaveChangesAsync();

            var second = NewHost("second.escritorio-a.local", SessionHostStatus.Online);
            context.SessionHosts.Add(second);
            await context.SaveChangesAsync();
        }

        await using var verify = NewContext();
        var host = await new RdsSessionBackend(verify).ResolveHostAsync(_applicationId);

        Assert.Equal("first.escritorio-a.local", host!.Fqdn);
    }

    [Fact]
    public async Task No_eligible_host_returns_null()
    {
        await using var context = NewContext();
        Assert.Null(await new RdsSessionBackend(context).ResolveHostAsync(_applicationId));
    }

    [Fact]
    public async Task An_unknown_application_returns_null()
    {
        await using var context = NewContext();
        Assert.Null(await new RdsSessionBackend(context).ResolveHostAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task BuildConnectionDescriptorAsync_produces_what_IRdpDescriptorBuilder_expects()
    {
        await using var context = NewContext();
        var host = NewHost("ab-rds01.escritorio-a.local", SessionHostStatus.Online);
        var application = await context.Applications.SingleAsync(a => a.Id == _applicationId);

        var descriptor = await new RdsSessionBackend(context).BuildConnectionDescriptorAsync(host, application);

        Assert.Equal("ab-rds01.escritorio-a.local", descriptor.HostAddress);
        Assert.Equal("dominio-contabil", descriptor.RemoteAppAlias);
        Assert.Equal("Domínio Contábil", descriptor.RemoteAppDisplayName);
    }
}
