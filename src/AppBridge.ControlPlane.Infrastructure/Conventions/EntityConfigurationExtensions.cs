using AppBridge.ControlPlane.Domain.Common;
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
}
