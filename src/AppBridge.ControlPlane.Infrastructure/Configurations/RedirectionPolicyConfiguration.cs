using AppBridge.ControlPlane.Domain.Tenancy;
using AppBridge.ControlPlane.Infrastructure.Conventions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppBridge.ControlPlane.Infrastructure.Configurations;

public sealed class RedirectionPolicyConfiguration : IEntityTypeConfiguration<RedirectionPolicy>
{
    public void Configure(EntityTypeBuilder<RedirectionPolicy> builder)
    {
        builder.ConfigureAuditedBase();

        // ADR-0008 condition 2: any application-specific override row must carry a reason. A
        // single-row CHECK cannot verify the values actually differ from the tenant baseline (that
        // would mean comparing against a different row) — see the comment on ExceptionReason.
        builder.ToTable("redirection_policy", tb => tb.HasCheckConstraint(
            "ck_redirection_policy_exception_reason",
            "application_id IS NULL OR exception_reason IS NOT NULL"));
    }
}
