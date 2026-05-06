using ThucLuc.Domain.Common.Base;

namespace ThucLuc.Domain.Entities.Business;

public sealed class HaTangMangHis : AuditableEntityBase
{
    public long SourceId { get; set; }

    public long DonViId { get; set; }

    public string LoaiDvThongKe { get; set; } = string.Empty;

    public int SoDonViTrucThuoc { get; set; }
    public int SoDaKetNoiBcanet { get; set; }
    public int SoDuongTruyenVnpt { get; set; }
    public int SoDuongTruyenKhac { get; set; }
    public int SoKetNoiInternet { get; set; }
    public string? GhiChu { get; set; }

    public string KyBaoCaoCode { get; set; } = string.Empty;
    public long? SnapshotBatchId { get; set; }
    public DateTime SnapshotCreatedAt { get; set; }
}
