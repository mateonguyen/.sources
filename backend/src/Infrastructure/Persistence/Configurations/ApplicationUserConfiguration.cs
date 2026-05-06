using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThucLuc.Domain.Entities.Identity;

namespace ThucLuc.Infrastructure.Persistence.Configurations;

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("IDM_USERS");
        builder.Property(x => x.HoTen).HasMaxLength(200).IsRequired();
        builder.Property(x => x.SoDienThoai).HasMaxLength(50);
        builder.Property(x => x.RefreshTokenHash).HasMaxLength(500);
        builder.HasOne(x => x.DonVi).WithMany().HasForeignKey(x => x.DonViId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.UserName).IsUnique();
        builder.HasIndex(x => x.Email);
    }
}