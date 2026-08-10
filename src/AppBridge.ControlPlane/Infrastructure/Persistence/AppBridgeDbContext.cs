using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using AppBridge.ControlPlane.Core.Entities;

namespace AppBridge.ControlPlane.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core DbContext for AppBridge Control Plane.
/// Multi-tenant isolation enforced at DbContext level (ADR-0004, ADR-0011).
/// All queries filtered by TenantId via global query filter.
/// Composite ForeignKeys include TenantId to prevent cross-tenant references (second line of defense).
/// </summary>
public class AppBridgeDbContext : DbContext
{
    private readonly string? _tenantId;

    /// <summary>
    /// Initialize with optional tenant ID for multi-tenant filtering.
    /// When TenantId is set, all queries are automatically filtered by this tenant.
    /// </summary>
    public AppBridgeDbContext(DbContextOptions<AppBridgeDbContext> options, string? tenantId = null)
        : base(options)
    {
        _tenantId = tenantId;
    }

    public DbSet<Tenant> Tenants { get; set; } = null!;
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Application> Applications { get; set; } = null!;
    public DbSet<Session> Sessions { get; set; } = null!;
    public DbSet<ApplicationUserPermission> ApplicationUserPermissions { get; set; } = null!;
    public DbSet<AuditLog> AuditLogs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ===== TENANT =====
        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Identifier).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.CreatedAt).HasColumnType("timestamptz");
            entity.Property(e => e.UpdatedAt).HasColumnType("timestamptz");
            entity.Property(e => e.DeletedAt).HasColumnType("timestamptz");

            // Unique identifier per tenant
            entity.HasIndex(e => e.Identifier).IsUnique();

            // Table
            entity.ToTable("tenants");
        });

        // ===== USER =====
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.TenantId).IsRequired();
            entity.Property(e => e.Identifier).HasMaxLength(256).IsRequired();
            entity.Property(e => e.DisplayName).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(256).IsRequired();
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnType("timestamptz");
            entity.Property(e => e.UpdatedAt).HasColumnType("timestamptz");
            entity.Property(e => e.LastLoginAt).HasColumnType("timestamptz");
            entity.Property(e => e.DeletedAt).HasColumnType("timestamptz");

            // Composite key: tenant + identifier (unique per tenant)
            entity.HasIndex(e => new { e.TenantId, e.Identifier }).IsUnique();

            // Foreign key with tenant_id composite constraint (ADR-0011)
            entity.HasOne(e => e.Tenant)
                .WithMany(e => e.Users)
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            // Table
            entity.ToTable("users");
        });

        // ===== APPLICATION =====
        modelBuilder.Entity<Application>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.TenantId).IsRequired();
            entity.Property(e => e.Identifier).HasMaxLength(100).IsRequired();
            entity.Property(e => e.DisplayName).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.RemoteAppName).HasMaxLength(255).IsRequired();
            entity.Property(e => e.IconUrl).HasMaxLength(2048);
            entity.Property(e => e.CreatedAt).HasColumnType("timestamptz");
            entity.Property(e => e.UpdatedAt).HasColumnType("timestamptz");
            entity.Property(e => e.DeletedAt).HasColumnType("timestamptz");

            // Composite key: tenant + identifier (unique per tenant)
            entity.HasIndex(e => new { e.TenantId, e.Identifier }).IsUnique();

            // Foreign key
            entity.HasOne(e => e.Tenant)
                .WithMany(e => e.Applications)
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            // Table
            entity.ToTable("applications");
        });

        // ===== SESSION =====
        modelBuilder.Entity<Session>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.TenantId).IsRequired();
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.ApplicationId).IsRequired();
            entity.Property(e => e.SessionHostId).HasMaxLength(100);
            entity.Property(e => e.State).HasConversion<string>();
            entity.Property(e => e.TerminationReason).HasMaxLength(500);
            entity.Property(e => e.RdpFileSignature).HasMaxLength(2000);
            entity.Property(e => e.StartedAt).HasColumnType("timestamptz");
            entity.Property(e => e.EndedAt).HasColumnType("timestamptz");
            entity.Property(e => e.CreatedAt).HasColumnType("timestamptz");
            entity.Property(e => e.UpdatedAt).HasColumnType("timestamptz");

            // Foreign keys
            entity.HasOne(e => e.Tenant)
                .WithMany(e => e.Sessions)
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.User)
                .WithMany(e => e.Sessions)
                .HasForeignKey(e => new { e.TenantId, e.UserId })
                .HasPrincipalKey(e => new { e.TenantId, e.Id })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Application)
                .WithMany(e => e.Sessions)
                .HasForeignKey(e => new { e.TenantId, e.ApplicationId })
                .HasPrincipalKey(e => new { e.TenantId, e.Id })
                .OnDelete(DeleteBehavior.Restrict);

            // Index for queries
            entity.HasIndex(e => new { e.TenantId, e.State, e.CreatedAt }).IsDescending(false, false, true);

            // Table
            entity.ToTable("sessions");
        });

        // ===== APPLICATION USER PERMISSION =====
        modelBuilder.Entity<ApplicationUserPermission>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.TenantId).IsRequired();
            entity.Property(e => e.ApplicationId).IsRequired();
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.GrantedAt).HasColumnType("timestamptz");
            entity.Property(e => e.ExpiresAt).HasColumnType("timestamptz");
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.RevokedAt).HasColumnType("timestamptz");
            entity.Property(e => e.RevocationReason).HasMaxLength(500);
            entity.Property(e => e.UpdatedAt).HasColumnType("timestamptz");

            // Composite unique key: (tenant_id, application_id, user_id)
            entity.HasIndex(e => new { e.TenantId, e.ApplicationId, e.UserId }).IsUnique();

            // Foreign keys with composite constraints
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Application)
                .WithMany(e => e.UserPermissions)
                .HasForeignKey(e => new { e.TenantId, e.ApplicationId })
                .HasPrincipalKey(e => new { e.TenantId, e.Id })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.User)
                .WithMany(e => e.ApplicationPermissions)
                .HasForeignKey(e => new { e.TenantId, e.UserId })
                .HasPrincipalKey(e => new { e.TenantId, e.Id })
                .OnDelete(DeleteBehavior.Restrict);

            // Table
            entity.ToTable("application_user_permissions");
        });

        // ===== AUDIT LOG =====
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.TenantId).IsRequired();
            entity.Property(e => e.Category).HasConversion<string>();
            entity.Property(e => e.ActorIdentifier).HasMaxLength(256).IsRequired();
            entity.Property(e => e.Action).HasMaxLength(100).IsRequired();
            entity.Property(e => e.ResourceType).HasMaxLength(100).IsRequired();
            entity.Property(e => e.ResourceId).HasMaxLength(36).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(1000).IsRequired();
            entity.Property(e => e.Result).HasConversion<string>();
            entity.Property(e => e.FailureReason).HasMaxLength(500);
            entity.Property(e => e.SourceIp).HasMaxLength(45); // IPv6
            entity.Property(e => e.UserAgent).HasMaxLength(500);
            entity.Property(e => e.Details).HasColumnType("jsonb");
            entity.Property(e => e.OccurredAt).HasColumnType("timestamptz");
            entity.Property(e => e.LoggedAt).HasColumnType("timestamptz");
            entity.Property(e => e.ChainHash).HasMaxLength(256); // SHA256 hex
            entity.Property(e => e.PreviousLogId);

            // Foreign key
            entity.HasOne(e => e.Tenant)
                .WithMany(e => e.AuditLogs)
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Actor)
                .WithMany()
                .HasForeignKey(e => e.ActorUserId)
                .OnDelete(DeleteBehavior.SetNull);

            // Indexes for queries (category + result for filtering, occurred_at for ordering)
            entity.HasIndex(e => new { e.TenantId, e.Category, e.OccurredAt }).IsDescending(false, false, true);
            entity.HasIndex(e => new { e.TenantId, e.ResourceType, e.ResourceId, e.OccurredAt }).IsDescending(false, false, false, true);
            entity.HasIndex(e => new { e.TenantId, e.Result, e.OccurredAt }).IsDescending(false, false, true);

            // Table
            entity.ToTable("audit_logs");
        });

        // ===== GLOBAL QUERY FILTERS (Multi-Tenant) =====
        // When TenantId is set, all queries automatically filter by this tenant (ADR-0004)
        if (!string.IsNullOrEmpty(_tenantId) && Guid.TryParse(_tenantId, out var tenantId))
        {
            modelBuilder.Entity<User>().HasQueryFilter(e => e.TenantId == tenantId);
            modelBuilder.Entity<Application>().HasQueryFilter(e => e.TenantId == tenantId);
            modelBuilder.Entity<Session>().HasQueryFilter(e => e.TenantId == tenantId);
            modelBuilder.Entity<ApplicationUserPermission>().HasQueryFilter(e => e.TenantId == tenantId);
            modelBuilder.Entity<AuditLog>().HasQueryFilter(e => e.TenantId == tenantId);
        }
    }
}

/// <summary>
/// Design-time factory for EF Core CLI (migrations).
/// Used by: dotnet ef migrations add, dotnet ef database update
/// </summary>
public class AppBridgeDbContextFactory : IDesignTimeDbContextFactory<AppBridgeDbContext>
{
    public AppBridgeDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppBridgeDbContext>();

        // Use development connection string (from appsettings.Development.json or user-secrets)
        var connectionString = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=appbridge_dev;Username=postgres;Password=postgres";

        optionsBuilder.UseNpgsql(connectionString, options =>
        {
            options.MigrationsHistoryTable("__ef_migrations_history");
        });

        return new AppBridgeDbContext(optionsBuilder.Build());
    }
}
