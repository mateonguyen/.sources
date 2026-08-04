using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThucLuc.Domain.Entities.Reporting;

namespace ThucLuc.Infrastructure.Persistence.Configurations;

public sealed class BaoCaoSnapshotXacNhanConfiguration : IEntityTypeConfiguration<BaoCaoSnapshotXacNhan>
{
    public void Configure(EntityTypeBuilder<BaoCaoSnapshotXacNhan> builder)
    {
        builder.ToTable("RPT_SNAPSHOT_XAC_NHAN");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.XacNhanAt).HasColumnName("XAC_NHAN_AT");
        builder.HasIndex(x => new { x.SnapshotId, x.DonViId }).IsUnique();
        builder.HasOne(x => x.Snapshot)
            .WithMany()
            .HasForeignKey(x => x.SnapshotId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.DonVi)
            .WithMany()
            .HasForeignKey(x => x.DonViId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}