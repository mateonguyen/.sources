using ThucLuc.Domain.Common.Base;

namespace ThucLuc.Domain.Entities.Business;

public sealed class GiamSatSocHis : AuditableEntityBase
{
    public long SourceId { get; set; }
    public long DonViId { get; set; }
    public string LoaiMang { get; set; } = null!;
    public string LopGiamSat { get; set; } = null!;
    public bool CoHeThong { get; set; }
    public string? ThucTrang { get; set; }
    public int TongSoDoiTuong { get; set; }
    public int SoGiamSatMotPhan { get; set; }
    public int SoGiamSatCoBan { get; set; }
    public int SoGiamSatDayDu { get; set; }
    public int SoSuCo { get; set; }
    public int SoSuCoDaKhacPhuc { get; set; }
    public string? LucLuongUngCuu { get; set; }
    public string? GhiChu { get; set; }
    public string KyBaoCaoCode { get; set; } = string.Empty;
    public long? SnapshotBatchId { get; set; }
    public DateTime SnapshotCreatedAt { get; set; }
}
