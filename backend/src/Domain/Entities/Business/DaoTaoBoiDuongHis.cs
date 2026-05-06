using ThucLuc.Domain.Common.Base;

namespace ThucLuc.Domain.Entities.Business;

public sealed class DaoTaoBoiDuongHis : AuditableEntityBase
{
    public long SourceId { get; set; }
    public long DonViId { get; set; }
    public string TenKhoaHoc { get; set; } = string.Empty;
    public string DonViToChuc { get; set; } = string.Empty;
    public string? HinhThuc { get; set; }
    public int? SoLuongHv { get; set; }
    public DateOnly? ThoiGianTu { get; set; }
    public DateOnly? ThoiGianDen { get; set; }
    public string? GhiChu { get; set; }
    public string KyBaoCaoCode { get; set; } = string.Empty;
    public long? SnapshotBatchId { get; set; }
    public DateTime SnapshotCreatedAt { get; set; }
}
