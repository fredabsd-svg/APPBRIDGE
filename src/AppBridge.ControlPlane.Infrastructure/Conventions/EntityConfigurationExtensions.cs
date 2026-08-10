using AppBridge.ControlPlane.Domain.Common;
using AppBridge.ControlPlane.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppBridge.ControlPlane.Infrastructure.Conventions;

public static class EntityConfigurationExtensions
{
    /// <summary>
    /// Common base wiring for a mutable, tenant-scoped entity: Id as the app-generated primary
    /// key (UUID v7, ADR-0011 §1 — never database-assigned).
    /// </summary>
    public static void ConfigureAuditedBase<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : AuditedEntity
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
    }

    /// <summary>Same as <see cref="ConfigureAuditedBase{TEntity}"/>, for the append-only trail tables.</summary>
    public static void ConfigureAppendOnlyBase<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : AppendOnlyEntity
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
    }

    /// <summary>
    /// <c>tenant_id -&gt; tenant(id)</c> — the simpler half of ADR-0011 §4: a row can't claim a
    /// tenant that doesn't exist. RESTRICT, not CASCADE: a tenant is deactivated
    /// (<see cref="TenantStatus"/>) or logically deleted, never physically removed while it still
    /// has data (ADR-0011 §3).
    /// </summary>
    public static void ConfigureTenantForeignKey<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : class, ITenantScoped
    {
        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .HasConstraintName($"fk_{SnakeCaseNamingConvention.ToSnakeCase(typeof(TEntity).Name)}_tenant")
            .OnDelete(DeleteBehavior.Restrict);
    }

    /// <summary>
    /// <c>UNIQUE (tenant_id, id)</c> — the candidate key ADR-0011 §4's composite foreign keys
    /// reference on the principal side ("a chave candidata que torna isso possível"). Only entities
    /// that another table actually points to need this; call it on those, not on every entity.
    /// </summary>
    public static void ConfigureTenantAlternateKey<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : TenantScopedEntity
    {
        builder.HasAlternateKey(e => new { e.TenantId, e.Id })
            .HasName($"uq_{SnakeCaseNamingConvention.ToSnakeCase(typeof(TEntity).Name)}_tenant");
    }
}
