using AppBridge.ControlPlane.Domain.Sessions;
using AppBridge.ControlPlane.Infrastructure.Conventions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppBridge.ControlPlane.Infrastructure.Configurations;

public sealed class HostPoolConfiguration : IEntityTypeConfiguration<HostPool>
{
    public void Configure(EntityTypeBuilder<HostPool> builder)
    {
        builder.ToTable("host_pool");
        builder.ConfigureAuditedBase();
        builder.ConfigureTenantForeignKey();
        builder.ConfigureTenantAlternateKey(); // referenced by application, session_host

        builder.Property(e => e.Name).IsRequired();
        builder.Property(e => e.BackendType).HasConversion(SnakeCaseEnumConverter.For<BackendType>()).IsRequired();
    }
}
