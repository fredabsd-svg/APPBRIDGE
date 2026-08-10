using AppBridge.ControlPlane.Domain.Catalog;
using AppBridge.ControlPlane.Domain.Identity;
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
        builder.ConfigureTenantForeignKey();

        builder.Property(e => e.EffectiveFrom).IsRequired();

        // The critical-path index (MODELO-DE-DADOS.md §5.2): RF-021 checks this on every launch.
        builder.HasIndex(e => new { e.TenantId, e.ApplicationId, e.GroupId })
            .HasFilter("effective_to IS NULL")
            .HasDatabaseName("ix_permission_active");

        // ADR-0011 §4 — the exact example the ADR itself gives: a permission can't grant access to
        // another tenant's application, group, or be attributed to another tenant's user.
        builder.HasOne<Application>()
            .WithMany()
            .HasForeignKey(e => new { e.TenantId, e.ApplicationId })
            .HasPrincipalKey(p => new { p.TenantId, p.Id })
            .HasConstraintName("fk_permission_application")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Group>()
            .WithMany()
            .HasForeignKey(e => new { e.TenantId, e.GroupId })
            .HasPrincipalKey(p => new { p.TenantId, p.Id })
            .HasConstraintName("fk_permission_group")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(e => new { e.TenantId, e.GrantedBy })
            .HasPrincipalKey(p => new { p.TenantId, p.Id })
            .HasConstraintName("fk_permission_granted_by")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(e => new { e.TenantId, e.RevokedBy })
            .HasPrincipalKey(p => new { p.TenantId, p.Id })
            .HasConstraintName("fk_permission_revoked_by")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
