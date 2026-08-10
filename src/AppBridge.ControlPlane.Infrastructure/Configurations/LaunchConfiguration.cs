using AppBridge.ControlPlane.Domain.Trail;
using AppBridge.ControlPlane.Infrastructure.Conventions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppBridge.ControlPlane.Infrastructure.Configurations;

public sealed class LaunchConfiguration : IEntityTypeConfiguration<Launch>
{
    public void Configure(EntityTypeBuilder<Launch> builder)
    {
        builder.ToTable("launch");
        builder.ConfigureAppendOnlyBase();

        builder.Property(e => e.RequestedAt).IsRequired();
        builder.Property(e => e.Outcome).HasConversion(SnakeCaseEnumConverter.For<LaunchOutcome>()).IsRequired();
        builder.Property(e => e.Purpose).HasConversion(SnakeCaseEnumConverter.For<LaunchPurpose>()).IsRequired();
        builder.Property(e => e.RdpExpiresAt).IsRequired();
        builder.Property(e => e.CorrelationId).IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.RequestedAt }).HasDatabaseName("ix_launch_tenant_requested_at");
    }
}
