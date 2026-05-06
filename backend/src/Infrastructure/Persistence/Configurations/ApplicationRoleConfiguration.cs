using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThucLuc.Domain.Entities.Identity;

namespace ThucLuc.Infrastructure.Persistence.Configurations;

public sealed class ApplicationRoleConfiguration : IEntityTypeConfiguration<ApplicationRole>
{
    public void Configure(EntityTypeBuilder<ApplicationRole> builder)
    {
        builder.ToTable("IDM_ROLES");
        builder.Property(x => x.TenRole).HasMaxLength(200).IsRequired();
        builder.Property(x => x.MoTa).HasMaxLength(500);
    }
}