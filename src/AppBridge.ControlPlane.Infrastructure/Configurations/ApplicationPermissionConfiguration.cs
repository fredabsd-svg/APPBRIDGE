using AppBridge.ControlPlane.Domain.Catalog;
using AppBridge.ControlPlane.Infrastructure.Conventions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppBridge.ControlPlane.Infrastructure.Configurations;

public sealed class ApplicationPermissionConfiguration : IEntityTypeConfiguration<ApplicationPermission>
{
    public void Configure(EntityTypeBuilder<ApplicationPermission> builder)
    {
        builder.ToTable("application_permission");
        builder.ConfigureAuditedBase();

        builder.Property(e => e.EffectiveFrom).IsRequired();

        // The critical-path index (MODELO-DE-DADOS.md §5.2): RF-021 checks this on every launch.
        builder.HasIndex(e => new { e.TenantId, e.ApplicationId, e.GroupId })
            .HasFilter("effective_to IS NULL")
            .HasDatabaseName("ix_permission_active");
    }
}
