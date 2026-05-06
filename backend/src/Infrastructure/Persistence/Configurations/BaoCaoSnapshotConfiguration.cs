using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThucLuc.Domain.Entities.Reporting;

namespace ThucLuc.Infrastructure.Persistence.Configurations;

public sealed class BaoCaoSnapshotConfiguration : IEntityTypeConfiguration<BaoCaoSnapshot>
{
    public void Configure(EntityTypeBuilder<BaoCaoSnapshot> builder)
    {
        builder.ToTable("RPT_BAO_CAO_SNAPSHOT");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.GhiChu).HasMaxLength(2000);
        builder.HasIndex(x => new { x.KyBaoCaoId, x.DonViId, x.PhienBan }).IsUnique();
        builder.HasOne(x => x.KyBaoCao).WithMany(x => x.Snapshots).HasForeignKey(x => x.KyBaoCaoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.DonVi).WithMany().HasForeignKey(x => x.DonViId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Files).WithOne(x => x.BaoCaoSnapshot).HasForeignKey(x => x.BaoCaoSnapshotId).OnDelete(DeleteBehavior.Cascade);
    }
}