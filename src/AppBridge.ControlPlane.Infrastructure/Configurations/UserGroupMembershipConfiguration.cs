using AppBridge.ControlPlane.Domain.Identity;
using AppBridge.ControlPlane.Infrastructure.Conventions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppBridge.ControlPlane.Infrastructure.Configurations;

public sealed class UserGroupMembershipConfiguration : IEntityTypeConfiguration<UserGroupMembership>
{
    public void Configure(EntityTypeBuilder<UserGroupMembership> builder)
    {
        builder.ToTable("user_group_membership");
        builder.ConfigureAuditedBase();
        builder.ConfigureTenantForeignKey();

        builder.Property(e => e.Source).HasConversion(SnakeCaseEnumConverter.For<GroupSource>()).IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.UserAccountId, e.GroupId }).IsUnique().HasDatabaseName("uq_user_group_membership_tenant_user_group");

        // ADR-0011 §4: membership can't cross tenants on either side.
        builder.HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(e => new { e.TenantId, e.UserAccountId })
            .HasPrincipalKey(p => new { p.TenantId, p.Id })
            .HasConstraintName("fk_user_group_membership_user_account")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Group>()
            .WithMany()
            .HasForeignKey(e => new { e.TenantId, e.GroupId })
            .HasPrincipalKey(p => new { p.TenantId, p.Id })
            .HasConstraintName("fk_user_group_membership_group")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
