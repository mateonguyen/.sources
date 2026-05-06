using ThucLuc.Domain.Common.Base;

namespace ThucLuc.Domain.Entities.Reporting;

public sealed class SnapshotBatch : AuditableEntityBase
{
    public long KyBaoCaoId { get; set; }
    public long DonViId { get; set; }
    public string Status { get; set; } = "RUNNING";
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public int TotalRows { get; set; }
    public string? ErrorMessage { get; set; }

    public KyBaoCao? KyBaoCao { get; set; }
}
