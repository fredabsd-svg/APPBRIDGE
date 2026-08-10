using AppBridge.ControlPlane.Domain.Identity;
using AppBridge.ControlPlane.Infrastructure.Conventions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppBridge.ControlPlane.Infrastructure.Configurations;

public sealed class GroupConfiguration : IEntityTypeConfiguration<Group>
{
    public void Configure(EntityTypeBuilder<Group> builder)
    {
        // "group" is a SQL reserved word; Npgsql quotes it correctly in every generated statement.
        builder.ToTable("group");
        builder.ConfigureAuditedBase();

        builder.Property(e => e.Name).IsRequired();
        builder.Property(e => e.Source).HasConversion(SnakeCaseEnumConverter.For<GroupSource>()).IsRequired();
    }
}
