using ThucLuc.Domain.Common.Base;

namespace ThucLuc.Domain.Entities.Business;

public sealed class GiamSatNocHis : AuditableEntityBase
{
    public long SourceId { get; set; }
    public long DonViId { get; set; }
    public string LopGiamSat { get; set; } = null!;
    public bool CoNoc { get; set; }
    public string? ThucTrang { get; set; }
    public int? NamThanhLap { get; set; }
    public int? SoNhanSu { get; set; }
    public string? GhiChu { get; set; }
    public string KyBaoCaoCode { get; set; } = string.Empty;
    public long? SnapshotBatchId { get; set; }
    public DateTime SnapshotCreatedAt { get; set; }
}
