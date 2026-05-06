using ThucLuc.Domain.Common.Base;

namespace ThucLuc.Domain.Entities.Business;

public sealed class GiamSatSoc : AuditableSoftDeleteEntityBase
{
    public long DonViId { get; set; }
    public string LoaiMang { get; set; } = null!;
    public string LopGiamSat { get; set; } = null!;
    public bool CoHeThong { get; set; }
    public string? ThucTrang { get; set; }
    public int? NamThanhLap { get; set; }
    public int? SoNhanSu { get; set; }
    public string? CongCuSuDung { get; set; }
    public int? SoCanhBaoThang { get; set; }
    public string? GhiChu { get; set; }
}
