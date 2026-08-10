using AppBridge.ControlPlane.Domain.Tenancy;
using AppBridge.ControlPlane.Infrastructure.Conventions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppBridge.ControlPlane.Infrastructure.Configurations;

public sealed class SigningCertificateConfiguration : IEntityTypeConfiguration<SigningCertificate>
{
    public void Configure(EntityTypeBuilder<SigningCertificate> builder)
    {
        builder.ToTable("signing_certificate");
        builder.ConfigureAuditedBase();

        builder.Property(e => e.Thumbprint).IsRequired();
        builder.Property(e => e.Subject).IsRequired();
        builder.Property(e => e.Issuer).IsRequired();
        builder.Property(e => e.Status).HasConversion(SnakeCaseEnumConverter.For<CertificateStatus>()).IsRequired();

        builder.HasIndex(e => e.Thumbprint).IsUnique().HasDatabaseName("uq_signing_certificate_thumbprint");
    }
}
