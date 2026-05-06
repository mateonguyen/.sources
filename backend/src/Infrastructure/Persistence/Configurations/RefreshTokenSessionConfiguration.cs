using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThucLuc.Domain.Entities.Identity;

namespace ThucLuc.Infrastructure.Persistence.Configurations;

public sealed class RefreshTokenSessionConfiguration : IEntityTypeConfiguration<RefreshTokenSession>
{
    public void Configure(EntityTypeBuilder<RefreshTokenSession> builder)
    {
        builder.ToTable("IDM_REFRESH_TOKEN_SESSIONS");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId).IsRequired();
        builder.Property(x => x.RefreshTokenHash).HasMaxLength(500).IsRequired();
        builder.Property(x => x.DeviceId).HasMaxLength(255).IsRequired();
        builder.Property(x => x.DeviceUserAgent).HasMaxLength(500).IsRequired();
        builder.Property(x => x.DeviceIpAddress).HasMaxLength(50).IsRequired();
        builder.Property(x => x.DeviceName).HasMaxLength(255);
        builder.Property(x => x.IssuedAt).IsRequired();
        builder.Property(x => x.ExpiresAt).IsRequired();
        builder.Property(x => x.LastUsedAt);
        builder.Property(x => x.IsRevoked).IsRequired();
        builder.Property(x => x.RevokedAt);
        builder.Property(x => x.RevocationReason).HasMaxLength(255);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        // Foreign key to ApplicationUser
        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(x => x.UserId).HasName("IX_RefreshTokenSession_UserId");
        builder.HasIndex(x => new { x.UserId, x.DeviceId }).HasName("IX_RefreshTokenSession_UserId_DeviceId");
        builder.HasIndex(x => x.IsRevoked).HasName("IX_RefreshTokenSession_IsRevoked");
        builder.HasIndex(x => x.ExpiresAt).HasName("IX_RefreshTokenSession_ExpiresAt");
    }
}
