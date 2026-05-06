using ThucLuc.Domain.Common.Base;

namespace ThucLuc.Domain.Entities.Business;

public sealed class NangLucSoHis : AuditableEntityBase
{
    public long SourceId { get; set; }
    public long DonViId { get; set; }
    public string NhomViTri { get; set; } = string.Empty;
    public int TongSoDienDanhGia { get; set; }
    public int TongSoDat { get; set; }
    public int TongSoChuaDat { get; set; }
    public string? GhiChu { get; set; }
    public string KyBaoCaoCode { get; set; } = string.Empty;
    public long? SnapshotBatchId { get; set; }
    public DateTime SnapshotCreatedAt { get; set; }
}
