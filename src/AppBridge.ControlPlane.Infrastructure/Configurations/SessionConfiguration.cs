using AppBridge.ControlPlane.Domain.Identity;
using AppBridge.ControlPlane.Domain.Sessions;
using AppBridge.ControlPlane.Infrastructure.Conventions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppBridge.ControlPlane.Infrastructure.Configurations;

public sealed class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ToTable("session");
        builder.ConfigureAuditedBase();
        builder.ConfigureTenantForeignKey();
        builder.ConfigureTenantAlternateKey(); // referenced by launch

        builder.Property(e => e.BackendSessionId).IsRequired();
        builder.Property(e => e.StartedAt).IsRequired();
        builder.Property(e => e.LastSeenAt).IsRequired();
        builder.Property(e => e.EndReason).HasConversion(SnakeCaseEnumConverter.For<SessionEndReason>());

        // Critical-path index: reconciliation (T-602) and the launch path both scan "which
        // sessions on this host are still open" (MODELO-DE-DADOS.md §6.2).
        builder.HasIndex(e => new { e.TenantId, e.SessionHostId })
            .HasFilter("ended_at IS NULL")
            .HasDatabaseName("ix_session_active");

        // ADR-0011 §4: a session can't be opened for another tenant's user or on another tenant's host.
        builder.HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(e => new { e.TenantId, e.UserAccountId })
            .HasPrincipalKey(p => new { p.TenantId, p.Id })
            .HasConstraintName("fk_session_user_account")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<SessionHost>()
            .WithMany()
            .HasForeignKey(e => new { e.TenantId, e.SessionHostId })
            .HasPrincipalKey(p => new { p.TenantId, p.Id })
            .HasConstraintName("fk_session_session_host")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
