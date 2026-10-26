using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThucLuc.Domain.Entities.Reporting;

namespace ThucLuc.Infrastructure.Persistence.Configurations;

public sealed class BaoCaoSnapshotMetricConfiguration : IEntityTypeConfiguration<BaoCaoSnapshotMetric>
{
    public void Configure(EntityTypeBuilder<BaoCaoSnapshotMetric> builder)
    {
        builder.ToTable("RPT_SNAPSHOT_METRIC");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ModuleCode).HasMaxLength(80).IsRequired();
        builder.Property(x => x.MetricCode).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Value).HasPrecision(24, 6);
        builder.Property(x => x.Numerator).HasPrecision(24, 6);
        builder.Property(x => x.Denominator).HasPrecision(24, 6);
        builder.HasIndex(x => new { x.SnapshotId, x.SourceDonViId, x.ModuleCode, x.MetricCode }).IsUnique();
        builder.HasIndex(x => new { x.KyBaoCaoId, x.ReportDonViId, x.ModuleCode });

        builder.HasOne(x => x.Snapshot)
            .WithMany()
            .HasForeignKey(x => x.SnapshotId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.SnapshotBatch)
            .WithMany()
            .HasForeignKey(x => x.SnapshotBatchId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.KyBaoCao)
            .WithMany()
            .HasForeignKey(x => x.KyBaoCaoId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ReportDonVi)
            .WithMany()
            .HasForeignKey(x => x.ReportDonViId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SourceDonVi)
            .WithMany()
            .HasForeignKey(x => x.SourceDonViId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
