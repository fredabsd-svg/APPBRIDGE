using AppBridge.ControlPlane.Domain.Tenancy;
using AppBridge.ControlPlane.Domain.Trail;
using AppBridge.ControlPlane.Infrastructure.Conventions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppBridge.ControlPlane.Infrastructure.Configurations;

public sealed class PurgeRunConfiguration : IEntityTypeConfiguration<PurgeRun>
{
    public void Configure(EntityTypeBuilder<PurgeRun> builder)
    {
        builder.ToTable("purge_run");
        builder.ConfigureAppendOnlyBase();

        builder.Property(e => e.Category).HasConversion(SnakeCaseEnumConverter.For<RetentionCategory>()).IsRequired();
        builder.Property(e => e.CutoffDate).IsRequired();
        builder.Property(e => e.StartedAt).IsRequired();
        builder.Property(e => e.Outcome).IsRequired();
    }
}
