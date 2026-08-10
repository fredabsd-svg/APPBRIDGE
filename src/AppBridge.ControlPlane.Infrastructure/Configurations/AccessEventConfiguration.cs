using AppBridge.ControlPlane.Domain.Trail;
using AppBridge.ControlPlane.Infrastructure.Conventions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppBridge.ControlPlane.Infrastructure.Configurations;

public sealed class AccessEventConfiguration : IEntityTypeConfiguration<AccessEvent>
{
    public void Configure(EntityTypeBuilder<AccessEvent> builder)
    {
        builder.ToTable("access_event");
        builder.ConfigureAppendOnlyBase();

        builder.Property(e => e.EventType).IsRequired();
        builder.Property(e => e.Result).HasConversion(SnakeCaseEnumConverter.For<AccessEventResult>()).IsRequired();
        builder.Property(e => e.OccurredAt).IsRequired();
        builder.Property(e => e.CorrelationId).IsRequired();
        builder.Property(e => e.Payload).HasColumnType("jsonb");

        builder.HasIndex(e => new { e.TenantId, e.OccurredAt }).HasDatabaseName("ix_access_event_tenant_occurred_at");
    }
}
