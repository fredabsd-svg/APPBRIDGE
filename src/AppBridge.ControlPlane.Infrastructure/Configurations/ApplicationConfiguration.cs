using AppBridge.ControlPlane.Domain.Catalog;
using AppBridge.ControlPlane.Domain.Sessions;
using AppBridge.ControlPlane.Infrastructure.Conventions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppBridge.ControlPlane.Infrastructure.Configurations;

public sealed class ApplicationConfiguration : IEntityTypeConfiguration<Application>
{
    public void Configure(EntityTypeBuilder<Application> builder)
    {
        builder.ToTable("application");
        builder.ConfigureAuditedBase();
        builder.ConfigureTenantForeignKey();
        builder.ConfigureTenantAlternateKey(); // referenced by redirection_policy, application_permission, launch

        builder.Property(e => e.DisplayName).IsRequired();
        builder.Property(e => e.RemoteAppAlias).IsRequired();
        builder.Property(e => e.LaunchMode).HasConversion(SnakeCaseEnumConverter.For<LaunchMode>()).IsRequired();
        builder.Property(e => e.Status).HasConversion(SnakeCaseEnumConverter.For<ApplicationStatus>()).IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.RemoteAppAlias, e.HostPoolId }).IsUnique().HasDatabaseName("uq_application_tenant_alias_pool");

        // ADR-0011 §4: an application can't be published into another tenant's host pool.
        builder.HasOne<HostPool>()
            .WithMany()
            .HasForeignKey(e => new { e.TenantId, e.HostPoolId })
            .HasPrincipalKey(p => new { p.TenantId, p.Id })
            .HasConstraintName("fk_application_host_pool")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
