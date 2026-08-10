using AppBridge.ControlPlane.Domain.Identity;
using AppBridge.ControlPlane.Infrastructure.Conventions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppBridge.ControlPlane.Infrastructure.Configurations;

public sealed class UserAccountConfiguration : IEntityTypeConfiguration<UserAccount>
{
    public void Configure(EntityTypeBuilder<UserAccount> builder)
    {
        builder.ToTable("user_account");
        builder.ConfigureAuditedBase();

        builder.Property(e => e.ExternalSubject).IsRequired();
        builder.Property(e => e.Upn).IsRequired();
        builder.Property(e => e.AdObjectSid).IsRequired();
        builder.Property(e => e.Status).HasConversion(SnakeCaseEnumConverter.For<UserAccountStatus>()).IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.ExternalSubject }).IsUnique().HasDatabaseName("uq_user_account_tenant_external_subject");
        builder.HasIndex(e => new { e.TenantId, e.AdObjectSid }).IsUnique().HasDatabaseName("uq_user_account_tenant_ad_object_sid");
    }
}
