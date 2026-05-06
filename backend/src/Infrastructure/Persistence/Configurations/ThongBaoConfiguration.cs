using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThucLuc.Domain.Entities.System;

namespace ThucLuc.Infrastructure.Persistence.Configurations;

public sealed class ThongBaoConfiguration : IEntityTypeConfiguration<ThongBao>
{
    public void Configure(EntityTypeBuilder<ThongBao> builder)
    {
        builder.ToTable("SYS_THONG_BAO");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TieuDe).HasMaxLength(255).IsRequired();
        builder.Property(x => x.NoiDung).HasMaxLength(4000).IsRequired();
    }
}