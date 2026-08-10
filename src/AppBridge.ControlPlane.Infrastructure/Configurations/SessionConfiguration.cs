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

        builder.Property(e => e.BackendSessionId).IsRequired();
        builder.Property(e => e.StartedAt).IsRequired();
        builder.Property(e => e.LastSeenAt).IsRequired();
        builder.Property(e => e.EndReason).HasConversion(SnakeCaseEnumConverter.For<SessionEndReason>());

        // Critical-path index: reconciliation (T-602) and the launch path both scan "which
        // sessions on this host are still open" (MODELO-DE-DADOS.md §6.2).
        builder.HasIndex(e => new { e.TenantId, e.SessionHostId })
            .HasFilter("ended_at IS NULL")
            .HasDatabaseName("ix_session_active");
    }
}
