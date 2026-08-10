using AppBridge.ControlPlane.Domain.Identity;
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
        builder.ConfigureTenantForeignKey();

        builder.Property(e => e.EventType).IsRequired();
        builder.Property(e => e.Result).HasConversion(SnakeCaseEnumConverter.For<AccessEventResult>()).IsRequired();
        builder.Property(e => e.OccurredAt).IsRequired();
        builder.Property(e => e.CorrelationId).IsRequired();
        builder.Property(e => e.Payload).HasColumnType("jsonb");

        builder.HasIndex(e => new { e.TenantId, e.OccurredAt }).HasDatabaseName("ix_access_event_tenant_occurred_at");

        // ADR-0011 §4. Null on a failed login against an unknown user (MODELO-DE-DADOS.md §7.2)
        // skips the FK check, matching that row's meaning — there is no account to point to yet.
        builder.HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(e => new { e.TenantId, e.UserAccountId })
            .HasPrincipalKey(p => new { p.TenantId, p.Id })
            .HasConstraintName("fk_access_event_user_account")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
