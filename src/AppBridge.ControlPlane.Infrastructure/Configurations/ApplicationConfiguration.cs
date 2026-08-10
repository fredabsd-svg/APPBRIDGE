using AppBridge.ControlPlane.Domain.Catalog;
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

        builder.Property(e => e.DisplayName).IsRequired();
        builder.Property(e => e.RemoteAppAlias).IsRequired();
        builder.Property(e => e.LaunchMode).HasConversion(SnakeCaseEnumConverter.For<LaunchMode>()).IsRequired();
        builder.Property(e => e.Status).HasConversion(SnakeCaseEnumConverter.For<ApplicationStatus>()).IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.RemoteAppAlias, e.HostPoolId }).IsUnique().HasDatabaseName("uq_application_tenant_alias_pool");
    }
}
