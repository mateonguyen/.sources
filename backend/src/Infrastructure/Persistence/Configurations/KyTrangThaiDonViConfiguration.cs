using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThucLuc.Domain.Entities.Reporting;

namespace ThucLuc.Infrastructure.Persistence.Configurations;

public sealed class KyTrangThaiDonViConfiguration : IEntityTypeConfiguration<KyTrangThaiDonVi>
{
    public void Configure(EntityTypeBuilder<KyTrangThaiDonVi> builder)
    {
        builder.ToTable("RPT_KY_TRANG_THAI_DON_VI");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DaXacNhan).HasColumnName("DA_XAC_NHAN").HasDefaultValue(false);
        builder.HasIndex(x => new { x.KyBaoCaoId, x.DonViId }).IsUnique();
        builder.HasOne(x => x.KyBaoCao).WithMany(x => x.TrangThaiDonVis).HasForeignKey(x => x.KyBaoCaoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.DonVi).WithMany().HasForeignKey(x => x.DonViId).OnDelete(DeleteBehavior.Restrict);
    }
}