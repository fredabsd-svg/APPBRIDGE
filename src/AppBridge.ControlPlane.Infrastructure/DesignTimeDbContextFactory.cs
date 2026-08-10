using AppBridge.ControlPlane.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AppBridge.ControlPlane.Infrastructure;

/// <summary>
/// Lets <c>dotnet ef</c> construct the context at design time (`migrations add`, `database
/// update`) without a host application. Reads the connection string from an environment variable,
/// never from a file — this factory only runs on a developer's machine via the CLI, and RP-06
/// applies here exactly as it does to application code.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppBridgeDbContext>
{
    public AppBridgeDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("APPBRIDGE_DB_CONNECTION")
            ?? throw new InvalidOperationException(
                "Set APPBRIDGE_DB_CONNECTION before running dotnet ef (see docs/SETUP-DEV.md).");

        var options = new DbContextOptionsBuilder<AppBridgeDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        // Migrations operate on DDL, not on DbSet queries — the tenant filter never applies to
        // them, so an unset TenantContext is correct here, not a workaround.
        return new AppBridgeDbContext(options, new TenantContext());
    }
}
