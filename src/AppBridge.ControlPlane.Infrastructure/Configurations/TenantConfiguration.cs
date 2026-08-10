using AppBridge.ControlPlane.Domain.Tenancy;
using AppBridge.ControlPlane.Infrastructure.Conventions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppBridge.ControlPlane.Infrastructure.Configurations;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenant");
        builder.ConfigureAuditedBase();

        builder.Property(e => e.Name).IsRequired();
        builder.Property(e => e.Slug).IsRequired();
        builder.Property(e => e.Status).HasConversion(SnakeCaseEnumConverter.For<TenantStatus>()).IsRequired();

        builder.HasIndex(e => e.Slug).IsUnique().HasDatabaseName("uq_tenant_slug");
    }
}
