using AppBridge.ControlPlane.Domain.Catalog;
using AppBridge.ControlPlane.Domain.Identity;
using AppBridge.ControlPlane.Domain.Sessions;
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
        builder.ConfigureTenantForeignKey();

        builder.Property(e => e.RequestedAt).IsRequired();
        builder.Property(e => e.Outcome).HasConversion(SnakeCaseEnumConverter.For<LaunchOutcome>()).IsRequired();
        builder.Property(e => e.Purpose).HasConversion(SnakeCaseEnumConverter.For<LaunchPurpose>()).IsRequired();
        builder.Property(e => e.RdpExpiresAt).IsRequired();
        builder.Property(e => e.CorrelationId).IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.RequestedAt }).HasDatabaseName("ix_launch_tenant_requested_at");

        // ADR-0011 §4: a trail row can't misattribute the launch to another tenant's user,
        // application or session — logical deletion (ADR-0011 §3) keeps those rows around
        // indefinitely, so the reference stays valid for as long as the trail itself does.
        builder.HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(e => new { e.TenantId, e.UserAccountId })
            .HasPrincipalKey(p => new { p.TenantId, p.Id })
            .HasConstraintName("fk_launch_user_account")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Application>()
            .WithMany()
            .HasForeignKey(e => new { e.TenantId, e.ApplicationId })
            .HasPrincipalKey(p => new { p.TenantId, p.Id })
            .HasConstraintName("fk_launch_application")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Session>()
            .WithMany()
            .HasForeignKey(e => new { e.TenantId, e.SessionId })
            .HasPrincipalKey(p => new { p.TenantId, p.Id })
            .HasConstraintName("fk_launch_session")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
