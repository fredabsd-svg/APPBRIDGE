using AppBridge.ControlPlane.Domain.Sessions;
using AppBridge.ControlPlane.Infrastructure.Conventions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppBridge.ControlPlane.Infrastructure.Configurations;

public sealed class SessionHostConfiguration : IEntityTypeConfiguration<SessionHost>
{
    public void Configure(EntityTypeBuilder<SessionHost> builder)
    {
        builder.ToTable("session_host");
        builder.ConfigureAuditedBase();
        builder.ConfigureTenantForeignKey();
        builder.ConfigureTenantAlternateKey(); // referenced by session

        builder.Property(e => e.Fqdn).IsRequired();
        builder.Property(e => e.Status).HasConversion(SnakeCaseEnumConverter.For<SessionHostStatus>()).IsRequired();

        // ADR-0011 §4: a session host can't belong to another tenant's pool, even if application
        // code gets the id wrong.
        builder.HasOne<HostPool>()
            .WithMany()
            .HasForeignKey(e => new { e.TenantId, e.HostPoolId })
            .HasPrincipalKey(p => new { p.TenantId, p.Id })
            .HasConstraintName("fk_session_host_host_pool")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
