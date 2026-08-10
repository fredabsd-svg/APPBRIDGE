using AppBridge.ControlPlane.Domain.Identity;
using AppBridge.ControlPlane.Infrastructure.Conventions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppBridge.ControlPlane.Infrastructure.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_token");
        builder.ConfigureAuditedBase();
        builder.ConfigureTenantForeignKey();

        builder.Property(e => e.TokenHash).IsRequired();
        builder.Property(e => e.ExpiresAt).IsRequired();

        builder.HasIndex(e => e.TokenHash).IsUnique().HasDatabaseName("uq_refresh_token_hash");

        // ADR-0011 §4: a refresh token can't be attributed to another tenant's user.
        builder.HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(e => new { e.TenantId, e.UserAccountId })
            .HasPrincipalKey(p => new { p.TenantId, p.Id })
            .HasConstraintName("fk_refresh_token_user_account")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
