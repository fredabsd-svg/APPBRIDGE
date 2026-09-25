using AppBridge.ControlPlane.Domain.Entities;
using AppBridge.ControlPlane.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AppBridge.ControlPlane.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options, TenantContext tenantContext) : DbContext(options)
{
    private readonly TenantContext _tenantContext = tenantContext;
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<RetentionPolicy> RetentionPolicies => Set<RetentionPolicy>();
    public DbSet<RedirectionPolicy> RedirectionPolicies => Set<RedirectionPolicy>();
    public DbSet<SigningCertificate> SigningCertificates => Set<SigningCertificate>();
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
    public DbSet<AppGroup> Groups => Set<AppGroup>();
    public DbSet<UserGroupMembership> UserGroupMemberships => Set<UserGroupMembership>();
    public DbSet<RemoteApplication> Applications => Set<RemoteApplication>();
    public DbSet<ApplicationPermission> ApplicationPermissions => Set<ApplicationPermission>();
    public DbSet<HostPool> HostPools => Set<HostPool>();
    public DbSet<SessionHost> SessionHosts => Set<SessionHost>();
    public DbSet<RemoteSession> Sessions => Set<RemoteSession>();
    public DbSet<Launch> Launches => Set<Launch>();
    public DbSet<AccessEvent> AccessEvents => Set<AccessEvent>();
    public DbSet<PurgeRun> PurgeRuns => Set<PurgeRun>();
    public DbSet<LaunchIdempotencyRecord> LaunchIdempotencyRecords => Set<LaunchIdempotencyRecord>();

    public override int SaveChanges() => SaveChanges(acceptAllChangesOnSuccess: true);

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        PrepareVersionedChanges();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => SaveChangesAsync(acceptAllChangesOnSuccess: true, cancellationToken);

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        PrepareVersionedChanges();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // As classes-base compartilham colunas; cada entidade concreta tem sua própria tabela.
        modelBuilder.Ignore<Entity>();
        modelBuilder.Ignore<MutableEntity>();
        modelBuilder.Ignore<TenantVersionedEntity>();
        modelBuilder.Ignore<TenantMutableEntity>();
        modelBuilder.Ignore<TenantAppendOnlyEntity>();

        ConfigureTenant(modelBuilder);
        ConfigureRetentionPolicy(modelBuilder);
        ConfigureRedirectionPolicy(modelBuilder);
        ConfigureSigningCertificate(modelBuilder);
        ConfigureUserAccount(modelBuilder);
        ConfigureGroup(modelBuilder);
        ConfigureUserGroupMembership(modelBuilder);
        ConfigureApplication(modelBuilder);
        ConfigureApplicationPermission(modelBuilder);
        ConfigureHostPool(modelBuilder);
        ConfigureSessionHost(modelBuilder);
        ConfigureSession(modelBuilder);
        ConfigureLaunch(modelBuilder);
        ConfigureAccessEvent(modelBuilder);
        ConfigurePurgeRun(modelBuilder);
        ConfigureLaunchIdempotency(modelBuilder);
        ConfigureTenantRelationships(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes().ToArray())
        {
            if (typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
            {
                ApplyTenantQueryFilter(modelBuilder, entityType.ClrType);
            }
            else if (entityType.ClrType == typeof(Tenant))
            {
                ApplyTenantRootQueryFilter(modelBuilder);
            }
        }

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.Name));
            }
        }
    }

    private static void ConfigureTenant(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Tenant>();
        ConfigureConcreteEntity(entity, "tenant");
        ConfigureMutableColumns(entity);
        entity.HasIndex(x => x.Slug).IsUnique().HasDatabaseName("uq_tenant_slug");
        entity.Property(x => x.Status).HasConversion(new EnumToSnakeCaseConverter<TenantStatus>()).HasColumnType("text");
        entity.ToTable("tenant", table => table.HasCheckConstraint(
            "ck_tenant_status_valid",
            "status IN ('active', 'suspended', 'terminated')"));
    }

    private static void ConfigureRetentionPolicy(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<RetentionPolicy>();
        ConfigureTenantMutableEntity(entity, "retention_policy");
        entity.HasIndex(x => new { x.TenantId, x.Category })
            .IsUnique()
            .HasDatabaseName("uq_retention_policy_tenant_id_category");
        entity.Property(x => x.Category).HasConversion(new EnumToSnakeCaseConverter<RetentionCategory>()).HasColumnType("text");
        entity.ToTable("retention_policy", table =>
        {
            table.HasCheckConstraint(
                "ck_retention_policy_category_valid",
                "category IN ('access', 'administrative', 'certificate_usage')");
            table.HasCheckConstraint(
                "ck_retention_policy_months_minimum",
                "(category = 'access' AND retention_months BETWEEN 6 AND 60) " +
                "OR (category = 'administrative' AND retention_months BETWEEN 12 AND 60) " +
                "OR (category = 'certificate_usage' AND retention_months >= 60)");
        });
    }

    private static void ConfigureRedirectionPolicy(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<RedirectionPolicy>();
        ConfigureTenantMutableEntity(entity, "redirection_policy");
        entity.HasIndex(x => x.TenantId)
            .IsUnique()
            .HasFilter("application_id IS NULL")
            .HasDatabaseName("uq_redirection_policy_tenant_default");
        entity.HasIndex(x => new { x.TenantId, x.ApplicationId })
            .IsUnique()
            .HasFilter("application_id IS NOT NULL")
            .HasDatabaseName("uq_redirection_policy_tenant_application");
        entity.Property(x => x.ApplicationId).IsRequired(false);
        entity.ToTable("redirection_policy");
    }

    private static void ConfigureSigningCertificate(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<SigningCertificate>();
        ConfigureTenantMutableEntity(entity, "signing_certificate");
        entity.HasIndex(x => x.Thumbprint).IsUnique().HasDatabaseName("uq_signing_certificate_thumbprint");
        entity.Property(x => x.Status)
            .HasConversion(new EnumToSnakeCaseConverter<SigningCertificateStatus>())
            .HasColumnType("text");
        entity.ToTable("signing_certificate", table => table.HasCheckConstraint(
            "ck_signing_certificate_status_valid",
            "status IN ('active', 'superseded', 'revoked')"));
    }

    private static void ConfigureUserAccount(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<UserAccount>();
        ConfigureTenantMutableEntity(entity, "user_account");
        entity.HasIndex(x => new { x.TenantId, x.ExternalSubject })
            .IsUnique()
            .HasDatabaseName("uq_user_account_tenant_external_subject");
        entity.HasIndex(x => new { x.TenantId, x.AdObjectSid })
            .IsUnique()
            .HasDatabaseName("uq_user_account_tenant_ad_object_sid");
        entity.Property(x => x.Status).HasConversion(new EnumToSnakeCaseConverter<UserAccountStatus>()).HasColumnType("text");
        entity.ToTable("user_account", table => table.HasCheckConstraint(
            "ck_user_account_status_valid",
            "status IN ('active', 'disabled')"));
    }

    private static void ConfigureGroup(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<AppGroup>();
        ConfigureTenantMutableEntity(entity, "group");
        entity.Property(x => x.Source).HasConversion(new EnumToSnakeCaseConverter<GroupSource>()).HasColumnType("text");
        entity.ToTable("group", table => table.HasCheckConstraint(
            "ck_group_source_valid",
            "source IN ('directory', 'local')"));
    }

    private static void ConfigureUserGroupMembership(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<UserGroupMembership>();
        ConfigureTenantMutableEntity(entity, "user_group_membership");
        entity.Property(x => x.Source).HasConversion(new EnumToSnakeCaseConverter<GroupSource>()).HasColumnType("text");
        entity.ToTable("user_group_membership", table => table.HasCheckConstraint(
            "ck_user_group_membership_source_valid",
            "source IN ('directory', 'local')"));
    }

    private static void ConfigureApplication(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<RemoteApplication>();
        ConfigureTenantMutableEntity(entity, "application");
        entity.HasIndex(x => new { x.TenantId, x.RemoteAppAlias, x.HostPoolId })
            .IsUnique()
            .HasDatabaseName("uq_application_tenant_remote_app_alias_host_pool");
        entity.Property(x => x.LaunchMode)
            .HasConversion(new EnumToSnakeCaseConverter<ApplicationLaunchMode>())
            .HasColumnType("text");
        entity.Property(x => x.Status).HasConversion(new EnumToSnakeCaseConverter<ApplicationStatus>()).HasColumnType("text");
        entity.ToTable("application", table =>
        {
            table.HasCheckConstraint(
                "ck_application_launch_mode_valid",
                "launch_mode IN ('remote_app', 'confined_desktop')");
            table.HasCheckConstraint(
                "ck_application_status_valid",
                "status IN ('draft', 'published', 'retired')");
        });
    }

    private static void ConfigureApplicationPermission(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ApplicationPermission>();
        ConfigureTenantVersionedEntity(entity, "application_permission");
        entity.HasIndex(x => new { x.TenantId, x.ApplicationId, x.GroupId })
            .HasFilter("effective_to IS NULL")
            .HasDatabaseName("ix_permission_active");
        entity.ToTable("application_permission");
    }

    private static void ConfigureHostPool(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<HostPool>();
        ConfigureTenantMutableEntity(entity, "host_pool");
        entity.Property(x => x.BackendType)
            .HasConversion(new EnumToSnakeCaseConverter<SessionBackendType>())
            .HasColumnType("text");
        entity.ToTable("host_pool", table => table.HasCheckConstraint(
            "ck_host_pool_backend_type_valid",
            "backend_type IN ('rds', 'avd')"));
    }

    private static void ConfigureSessionHost(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<SessionHost>();
        ConfigureTenantMutableEntity(entity, "session_host");
        entity.Property(x => x.Status)
            .HasConversion(new EnumToSnakeCaseConverter<SessionHostStatus>())
            .HasColumnType("text");
        entity.ToTable("session_host", table => table.HasCheckConstraint(
            "ck_session_host_status_valid",
            "status IN ('online', 'draining', 'offline')"));
    }

    private static void ConfigureSession(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<RemoteSession>();
        ConfigureTenantMutableEntity(entity, "session");
        entity.HasIndex(x => new { x.TenantId, x.SessionHostId })
            .HasFilter("ended_at IS NULL")
            .HasDatabaseName("ix_session_active");
        entity.Property(x => x.EndReason)
            .HasConversion(new NullableEnumToSnakeCaseConverter<SessionEndReason>())
            .HasColumnType("text");
        entity.ToTable("session", table => table.HasCheckConstraint(
            "ck_session_end_reason_valid",
            "end_reason IS NULL OR end_reason IN ('logoff', 'disconnect_timeout', 'terminated_by_admin', 'revoked', 'reconciled_missing', 'stale_expired')"));
    }

    private static void ConfigureLaunch(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Launch>();
        ConfigureTenantAppendOnlyEntity(entity, "launch");
        entity.HasIndex(x => new { x.TenantId, x.RequestedAt }).HasDatabaseName("ix_launch_tenant_requested_at");
        entity.Property(x => x.Purpose).HasConversion(new EnumToSnakeCaseConverter<LaunchPurpose>()).HasColumnType("text");
        entity.Property(x => x.Outcome).HasConversion(new EnumToSnakeCaseConverter<LaunchOutcome>()).HasColumnType("text");
        entity.ToTable("launch", table =>
        {
            table.HasCheckConstraint(
                "ck_launch_purpose_valid",
                "purpose IN ('user_initiated', 'prelaunch')");
            table.HasCheckConstraint(
                "ck_launch_outcome_valid",
                "outcome IN ('granted', 'denied_permission', 'denied_quota', 'denied_host_unavailable', 'error_signing', 'error_internal')");
        });
    }

    private static void ConfigureAccessEvent(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<AccessEvent>();
        ConfigureTenantAppendOnlyEntity(entity, "access_event");
        entity.HasIndex(x => new { x.TenantId, x.OccurredAt }).HasDatabaseName("ix_access_event_tenant_occurred_at");
        entity.Property(x => x.EventType)
            .HasConversion(new EnumToSnakeCaseConverter<AccessEventType>())
            .HasColumnType("text");
        entity.Property(x => x.Result).HasConversion(new EnumToSnakeCaseConverter<AccessEventResult>()).HasColumnType("text");
        entity.Property(x => x.Payload).HasColumnType("jsonb");
        entity.ToTable("access_event", table =>
        {
            table.HasCheckConstraint(
                "ck_access_event_event_type_valid",
                "event_type IN ('authentication', 'logout', 'session_started', 'session_ended')");
            table.HasCheckConstraint(
                "ck_access_event_result_valid",
                "result IN ('success', 'failure')");
        });
    }

    private static void ConfigurePurgeRun(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<PurgeRun>();
        ConfigureTenantAppendOnlyEntity(entity, "purge_run");
        entity.Property(x => x.Category)
            .HasConversion(new EnumToSnakeCaseConverter<RetentionCategory>())
            .HasColumnType("text");
        entity.ToTable("purge_run", table => table.HasCheckConstraint(
            "ck_purge_run_category_valid",
            "category IN ('access', 'administrative', 'certificate_usage')"));
    }

    private static void ConfigureLaunchIdempotency(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<LaunchIdempotencyRecord>();
        ConfigureTenantMutableEntity(entity, "launch_idempotency");
        entity.HasIndex(x => new { x.TenantId, x.IdempotencyKey })
            .IsUnique()
            .HasDatabaseName("uq_launch_idempotency_tenant_key");
        entity.HasIndex(x => new { x.TenantId, x.ExpiresAt })
            .HasDatabaseName("ix_launch_idempotency_tenant_expires_at");
        entity.Property(x => x.ResponseJson).HasColumnType("jsonb");
        entity.ToTable("launch_idempotency");
    }

    private static void ConfigureTenantRelationships(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RedirectionPolicy>()
            .HasOne<RemoteApplication>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.ApplicationId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_redirection_policy_application");

        modelBuilder.Entity<UserGroupMembership>()
            .HasOne<UserAccount>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.UserAccountId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_user_group_membership_user_account");
        modelBuilder.Entity<UserGroupMembership>()
            .HasOne<AppGroup>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.GroupId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_user_group_membership_group");

        modelBuilder.Entity<RemoteApplication>()
            .HasOne<HostPool>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.HostPoolId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_application_host_pool");

        modelBuilder.Entity<ApplicationPermission>()
            .HasOne<RemoteApplication>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.ApplicationId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_application_permission_application");
        modelBuilder.Entity<ApplicationPermission>()
            .HasOne<AppGroup>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.GroupId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_application_permission_group");
        modelBuilder.Entity<ApplicationPermission>()
            .HasOne<UserAccount>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.GrantedBy })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_application_permission_granted_by");
        modelBuilder.Entity<ApplicationPermission>()
            .HasOne<UserAccount>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.RevokedBy })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_application_permission_revoked_by");

        modelBuilder.Entity<SessionHost>()
            .HasOne<HostPool>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.HostPoolId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_session_host_host_pool");
        modelBuilder.Entity<RemoteSession>()
            .HasOne<UserAccount>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.UserAccountId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_session_user_account");
        modelBuilder.Entity<RemoteSession>()
            .HasOne<SessionHost>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.SessionHostId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_session_session_host");

        modelBuilder.Entity<Launch>()
            .HasOne<UserAccount>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.UserAccountId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_launch_user_account");
        modelBuilder.Entity<Launch>()
            .HasOne<RemoteApplication>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.ApplicationId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_launch_application");
        modelBuilder.Entity<Launch>()
            .HasOne<RemoteSession>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.SessionId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_launch_session");
        modelBuilder.Entity<AccessEvent>()
            .HasOne<UserAccount>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.UserAccountId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_access_event_user_account");
    }

    private void ApplyTenantQueryFilter(ModelBuilder modelBuilder, Type entityType)
    {
        var parameter = System.Linq.Expressions.Expression.Parameter(entityType, "entity");
        var tenantIdProperty = System.Linq.Expressions.Expression.Property(parameter, nameof(ITenantEntity.TenantId));
        var context = System.Linq.Expressions.Expression.Constant(this);
        var tenantContext = System.Linq.Expressions.Expression.Field(context, nameof(_tenantContext));
        var hasTenant = System.Linq.Expressions.Expression.Property(tenantContext, nameof(TenantContext.IsBound));
        var tenantValue = System.Linq.Expressions.Expression.Property(tenantContext, nameof(TenantContext.EffectiveTenantId));
        var tenantMatches = System.Linq.Expressions.Expression.Equal(tenantIdProperty, tenantValue);
        System.Linq.Expressions.Expression filter = System.Linq.Expressions.Expression.AndAlso(hasTenant, tenantMatches);

        var deletedAt = entityType.GetProperty(nameof(MutableEntity.DeletedAt));
        if (deletedAt is not null)
        {
            var notDeleted = System.Linq.Expressions.Expression.Equal(
                System.Linq.Expressions.Expression.Property(parameter, deletedAt),
                System.Linq.Expressions.Expression.Constant(null, typeof(DateTimeOffset?)));
            filter = System.Linq.Expressions.Expression.AndAlso(filter, notDeleted);
        }

        modelBuilder.Entity(entityType).HasQueryFilter(
            System.Linq.Expressions.Expression.Lambda(filter, parameter));
    }

    private void ApplyTenantRootQueryFilter(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>().HasQueryFilter(tenant =>
            _tenantContext.IsBound
            && tenant.Id == _tenantContext.EffectiveTenantId
            && tenant.DeletedAt == null);
    }

    private static void ConfigureConcreteEntity<TEntity>(EntityTypeBuilder<TEntity> entity, string tableName)
        where TEntity : Entity
    {
        entity.HasBaseType((Type?)null);
        entity.ToTable(tableName);
        entity.HasKey(x => x.Id).HasName($"pk_{tableName}");
        entity.Property(x => x.Id).ValueGeneratedNever();
    }

    private static void ConfigureMutableColumns<TEntity>(EntityTypeBuilder<TEntity> entity)
        where TEntity : MutableEntity
    {
        entity.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        entity.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        entity.Property(x => x.RowVersion).HasDefaultValue(1L).IsConcurrencyToken();
    }

    private static void ConfigureTenantMutableEntity<TEntity>(EntityTypeBuilder<TEntity> entity, string tableName)
        where TEntity : TenantMutableEntity
    {
        ConfigureConcreteEntity(entity, tableName);
        ConfigureMutableColumns(entity);
        ConfigureTenantColumns(entity, tableName);
    }

    private static void ConfigureTenantAppendOnlyEntity<TEntity>(EntityTypeBuilder<TEntity> entity, string tableName)
        where TEntity : TenantAppendOnlyEntity
    {
        ConfigureConcreteEntity(entity, tableName);
        entity.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        ConfigureTenantColumns(entity, tableName);
    }

    private static void ConfigureTenantVersionedEntity<TEntity>(EntityTypeBuilder<TEntity> entity, string tableName)
        where TEntity : TenantVersionedEntity
    {
        ConfigureConcreteEntity(entity, tableName);
        entity.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        entity.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        entity.Property(x => x.RowVersion).HasDefaultValue(1L).IsConcurrencyToken();
        ConfigureTenantColumns(entity, tableName);
    }

    private static void ConfigureTenantColumns<TEntity>(EntityTypeBuilder<TEntity> entity, string tableName)
        where TEntity : Entity, ITenantEntity
    {
        entity.HasAlternateKey(x => new { x.TenantId, x.Id })
            .HasName($"uq_{tableName}_tenant_id_id");
        entity.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName($"fk_{tableName}_tenant");
    }

    private void PrepareVersionedChanges()
    {
        if (ChangeTracker.Entries().Any(entry =>
                entry.Entity is TenantAppendOnlyEntity
                && entry.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException("Registros de trilha são append-only e não podem ser alterados ou removidos.");
        }

        foreach (var entry in ChangeTracker.Entries().Where(entry =>
                     entry.State is EntityState.Added or EntityState.Modified && entry.Entity is ITenantEntity))
        {
            var tenantEntity = (ITenantEntity)entry.Entity;
            if (_tenantContext.TenantId is null)
            {
                throw new InvalidOperationException("Não é permitido gravar dados de tenant sem TenantContext.");
            }

            if (tenantEntity.TenantId == Guid.Empty)
            {
                tenantEntity.TenantId = _tenantContext.TenantId.Value;
            }
            else if (tenantEntity.TenantId != _tenantContext.TenantId.Value)
            {
                throw new InvalidOperationException("TenantId da entidade não corresponde ao TenantContext.");
            }
        }

        foreach (var entry in ChangeTracker.Entries().Where(entry =>
                     entry.State == EntityState.Modified && entry.Entity is IVersionedEntity))
        {
            var versionedEntity = (IVersionedEntity)entry.Entity;
            var rowVersion = entry.Property(nameof(IVersionedEntity.RowVersion));
            versionedEntity.RowVersion = (long)rowVersion.OriginalValue! + 1;
            versionedEntity.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    private static string ToSnakeCase(string value)
    {
        var result = new System.Text.StringBuilder(value.Length + 8);
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (char.IsUpper(character) && index > 0 && (char.IsLower(value[index - 1]) || char.IsDigit(value[index - 1])))
            {
                result.Append('_');
            }

            result.Append(char.ToLowerInvariant(character));
        }

        return result.ToString();
    }
}

internal sealed class EnumToSnakeCaseConverter<TEnum>() : ValueConverter<TEnum, string>(
    value => EnumText.ToSnakeCase(value.ToString()),
    value => EnumText.Parse<TEnum>(value))
    where TEnum : struct, Enum;

internal sealed class NullableEnumToSnakeCaseConverter<TEnum>() : ValueConverter<TEnum?, string?>(
    value => value.HasValue ? EnumText.ToSnakeCase(value.Value.ToString()) : null,
    value => string.IsNullOrWhiteSpace(value) ? null : EnumText.Parse<TEnum>(value))
    where TEnum : struct, Enum;

internal static class EnumText
{
    public static string ToSnakeCase(string value)
    {
        var result = new System.Text.StringBuilder(value.Length + 8);
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (char.IsUpper(character) && index > 0)
            {
                result.Append('_');
            }

            result.Append(char.ToLowerInvariant(character));
        }

        return result.ToString();
    }

    public static TEnum Parse<TEnum>(string value) where TEnum : struct, Enum
    {
        var enumName = value
            .Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Aggregate(new System.Text.StringBuilder(), (builder, part) =>
            {
                builder.Append(char.ToUpperInvariant(part[0]));
                builder.Append(part.AsSpan(1));
                return builder;
            })
            .ToString();

        return Enum.Parse<TEnum>(enumName, ignoreCase: false);
    }
}
