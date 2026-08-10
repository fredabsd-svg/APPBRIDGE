using AppBridge.ControlPlane.Domain.Tenancy;
using AppBridge.ControlPlane.Infrastructure.Conventions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppBridge.ControlPlane.Infrastructure.Configurations;

public sealed class RetentionPolicyConfiguration : IEntityTypeConfiguration<RetentionPolicy>
{
    public void Configure(EntityTypeBuilder<RetentionPolicy> builder)
    {
        builder.ConfigureAuditedBase();
        builder.ConfigureTenantForeignKey();

        builder.Property(e => e.Category).HasConversion(SnakeCaseEnumConverter.For<RetentionCategory>()).IsRequired();
        builder.Property(e => e.RetentionMonths).IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.Category }).IsUnique().HasDatabaseName("uq_retention_policy_tenant_category");

        // ADR-0007 minimums, enforced in the database — not just in the application — because the
        // protection exists specifically against a caller instructing a value below it.
        builder.ToTable("retention_policy", tb => tb.HasCheckConstraint(
            "ck_retention_policy_minimum",
            "(category = 'access' AND retention_months BETWEEN 6 AND 60) OR "
                + "(category = 'administrative' AND retention_months BETWEEN 12 AND 60) OR "
                + "(category = 'certificate_usage' AND retention_months >= 60)"));
    }
}
