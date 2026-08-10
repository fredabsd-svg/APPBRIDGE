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

        builder.Property(e => e.Source).HasConversion(SnakeCaseEnumConverter.For<GroupSource>()).IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.UserAccountId, e.GroupId }).IsUnique().HasDatabaseName("uq_user_group_membership_tenant_user_group");
    }
}
