using System.Reflection;
using AppBridge.ControlPlane.Domain.Catalog;
using AppBridge.ControlPlane.Domain.Identity;
using AppBridge.ControlPlane.Domain.Sessions;
using AppBridge.ControlPlane.Domain.Tenancy;
using AppBridge.ControlPlane.Domain.Trail;
using Microsoft.EntityFrameworkCore;
using AppBridge.ControlPlane.Infrastructure.Conventions;

namespace AppBridge.ControlPlane.Infrastructure;

/// <summary>
/// T-202 scope: schema and migrations only. The tenant isolation filter (ADR-0004) is T-203's —
/// wiring it in here now, before <c>TenantContext</c> exists to feed it, would mean a filter with
/// nothing driving it, which is worse than no filter: it would look like isolation is already
/// enforced when it is not.
/// </summary>
public sealed class AppBridgeDbContext(DbContextOptions<AppBridgeDbContext> options) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<RetentionPolicy> RetentionPolicies => Set<RetentionPolicy>();

    public DbSet<RedirectionPolicy> RedirectionPolicies => Set<RedirectionPolicy>();

    public DbSet<SigningCertificate> SigningCertificates => Set<SigningCertificate>();

    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();

    public DbSet<Group> Groups => Set<Group>();

    public DbSet<UserGroupMembership> UserGroupMemberships => Set<UserGroupMembership>();

    public DbSet<Application> Applications => Set<Application>();

    public DbSet<ApplicationPermission> ApplicationPermissions => Set<ApplicationPermission>();

    public DbSet<HostPool> HostPools => Set<HostPool>();

    public DbSet<SessionHost> SessionHosts => Set<SessionHost>();

    public DbSet<Session> Sessions => Set<Session>();

    public DbSet<Launch> Launches => Set<Launch>();

    public DbSet<AccessEvent> AccessEvents => Set<AccessEvent>();

    public DbSet<PurgeRun> PurgeRuns => Set<PurgeRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Must run after the configurations above, which is why it's last: it walks the model
        // they just built and renames every column (ADR-0011 §6).
        SnakeCaseNamingConvention.Apply(modelBuilder);

        // Must run after the snake_case pass — see the comment on the convention itself.
        ConcurrencyTokenConvention.Apply(modelBuilder);
    }
}
