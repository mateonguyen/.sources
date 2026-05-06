using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThucLuc.Domain.Entities.Identity;

namespace ThucLuc.Infrastructure.Persistence.Configurations;

public sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("IDM_PERMISSIONS");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PermCode).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Module).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Action).HasMaxLength(100).IsRequired();
        builder.Property(x => x.MoTa).HasMaxLength(500);
        builder.HasIndex(x => x.PermCode).IsUnique();
    }
}