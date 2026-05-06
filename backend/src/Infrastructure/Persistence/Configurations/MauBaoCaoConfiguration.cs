using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThucLuc.Domain.Entities.Reporting;

namespace ThucLuc.Infrastructure.Persistence.Configurations;

public sealed class MauBaoCaoConfiguration : IEntityTypeConfiguration<MauBaoCao>
{
    public void Configure(EntityTypeBuilder<MauBaoCao> builder)
    {
        builder.ToTable("RPT_MAU_BAO_CAO");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.MaMau).HasMaxLength(50).IsRequired();
        builder.Property(x => x.TenMau).HasMaxLength(200).IsRequired();
        builder.Property(x => x.MoTa).HasMaxLength(1000);
        builder.Property(x => x.DanhSachModule).HasColumnType("NCLOB").IsRequired();

        builder.HasIndex(x => x.MaMau).IsUnique();

        builder.HasMany(x => x.KyBaoCaos)
               .WithOne(x => x.MauBaoCao)
               .HasForeignKey(x => x.MauBaoCaoId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
