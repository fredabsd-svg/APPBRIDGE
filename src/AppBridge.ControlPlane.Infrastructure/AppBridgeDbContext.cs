using System.Reflection;
using AppBridge.ControlPlane.Domain.Catalog;
using AppBridge.ControlPlane.Domain.Common;
using AppBridge.ControlPlane.Domain.Identity;
using AppBridge.ControlPlane.Domain.Sessions;
using AppBridge.ControlPlane.Domain.Tenancy;
using AppBridge.ControlPlane.Domain.Trail;
using Microsoft.EntityFrameworkCore;
using AppBridge.ControlPlane.Infrastructure.Conventions;
using AppBridge.ControlPlane.Infrastructure.Tenancy;

namespace AppBridge.ControlPlane.Infrastructure;

public sealed class AppBridgeDbContext(DbContextOptions<AppBridgeDbContext> options, ITenantContext tenantContext)
    : DbContext(options)
{
    private readonly ITenantContext _tenantContext = tenantContext;


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

        ApplyTenantAndSoftDeleteFilters(modelBuilder);
    }

    /// <summary>
    /// ADR-0004 item 6: every query is scoped to <see cref="_tenantContext"/> by default, not by a
    /// clause the caller remembered to write. ADR-0011 §5 (consequences) decided the soft-delete
    /// filter belongs in the same place, for the same reason — "toda consulta considere
    /// <c>deleted_at</c>, o que se resolve por filtro global no DbContext, junto com o filtro de
    /// tenant". <c>Tenant</c> and <c>SigningCertificate</c> are not <see cref="ITenantScoped"/> and
    /// get the soft-delete half only. Trail tables (<see cref="AppendOnlyEntity"/>) have no
    /// <c>deleted_at</c> at all (ADR-0011 §3) and get the tenant half only.
    ///
    /// A caller that genuinely needs to cross tenants or see soft-deleted rows calls
    /// <c>IgnoreQueryFilters()</c> explicitly — that explicitness is the point of ADR-0004 item 7,
    /// not a gap in this filter.
    /// </summary>
    private void ApplyTenantAndSoftDeleteFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;
            var isTenantScoped = typeof(ITenantScoped).IsAssignableFrom(clrType);
            var isSoftDeletable = typeof(AuditedEntity).IsAssignableFrom(clrType);

            var methodName = (isTenantScoped, isSoftDeletable) switch
            {
                (true, true) => nameof(SetTenantAndSoftDeleteFilter),
                (true, false) => nameof(SetTenantFilter),
                (false, true) => nameof(SetSoftDeleteFilter),
                (false, false) => null,
            };

            if (methodName is null)
            {
                continue;
            }

            var method = GetType()
                .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(clrType);
            method.Invoke(this, [modelBuilder]);
        }
    }

    private void SetTenantAndSoftDeleteFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : AuditedEntity, ITenantScoped
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => e.TenantId == _tenantContext.TenantId && e.DeletedAt == null);
    }

    private void SetTenantFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantScoped
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);
    }

    private void SetSoftDeleteFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : AuditedEntity
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => e.DeletedAt == null);
    }
}
