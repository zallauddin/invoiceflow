using InvoiceFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvoiceFlow.Infrastructure.Auth;

/// <summary>EF Core configuration for RefreshToken entity.</summary>
public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(rt => rt.Id);

        builder.Property(rt => rt.Token)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(rt => rt.UserId)
            .IsRequired();

        builder.Property(rt => rt.TenantId)
            .IsRequired();

        builder.Property(rt => rt.ExpiresAt)
            .IsRequired();

        builder.Property(rt => rt.CreatedAt)
            .IsRequired();

        builder.Property(rt => rt.RevokedAt)
            .IsRequired(false);

        // Ignore computed properties — no backing field, not persisted
        builder.Ignore(rt => rt.IsActive);
        builder.Ignore(rt => rt.IsRevoked);
        builder.Ignore(rt => rt.IsExpired);

        // Index on token for fast lookup during refresh
        builder.HasIndex(rt => rt.Token)
            .IsUnique()
            .HasDatabaseName("IX_refresh_tokens_token");

        // Index on UserId for bulk revocation
        builder.HasIndex(rt => rt.UserId)
            .HasDatabaseName("IX_refresh_tokens_user_id");

        // Composite index for active token lookups (UserId + RevokedAt)
        builder.HasIndex(rt => new { rt.UserId, rt.RevokedAt })
            .HasDatabaseName("IX_refresh_tokens_user_revoked");
    }
}
