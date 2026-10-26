using ThucLuc.Domain.Common.Base;
using ThucLuc.Domain.Entities.System;

namespace ThucLuc.Domain.Entities.Reporting;

/// <summary>
/// Chỉ số nghiệp vụ bất biến được tính tại thời điểm chốt snapshot.
/// ReportDonViId là đơn vị nộp báo cáo; SourceDonViId là đơn vị phát sinh dữ liệu.
/// </summary>
public sealed class BaoCaoSnapshotMetric : AuditableEntityBase
{
    public long SnapshotId { get; set; }
    public long SnapshotBatchId { get; set; }
    public long KyBaoCaoId { get; set; }
    public long ReportDonViId { get; set; }
    public long SourceDonViId { get; set; }
    public string ModuleCode { get; set; } = string.Empty;
    public string MetricCode { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public decimal? Numerator { get; set; }
    public decimal? Denominator { get; set; }
    public int MetricVersion { get; set; } = 1;

    public BaoCaoSnapshot? Snapshot { get; set; }
    public SnapshotBatch? SnapshotBatch { get; set; }
    public KyBaoCao? KyBaoCao { get; set; }
    public DonVi? ReportDonVi { get; set; }
    public DonVi? SourceDonVi { get; set; }
}
