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

        builder.Property(e => e.Fqdn).IsRequired();
        builder.Property(e => e.Status).HasConversion(SnakeCaseEnumConverter.For<SessionHostStatus>()).IsRequired();
    }
}
