using ThucLuc.Domain.Common.Base;

namespace ThucLuc.Domain.Entities.Business;

public sealed class AtttHtttDauTuHis : AuditableEntityBase
{
    public long SourceId { get; set; }
    public long DonViId { get; set; }
    public long HtttId { get; set; }
    public string? ChuQuan { get; set; }
    public string? DonViVanHanh { get; set; }
    public string? CapDoDeXuat { get; set; }
    public DateOnly? NgayPheDuyetHsdxcd { get; set; }
    public string? QuyetDinhPheDuyet { get; set; }
    public bool DaLongGhepThuyetMinh { get; set; }
    public string? GhiChu { get; set; }
    public string KyBaoCaoCode { get; set; } = string.Empty;
    public long? SnapshotBatchId { get; set; }
    public DateTime SnapshotCreatedAt { get; set; }
}
