using ThucLuc.Domain.Common.Base;

namespace ThucLuc.Domain.Entities.Business;

public sealed class HaTangMang : AuditableSoftDeleteEntityBase
{
    public long DonViId { get; set; }

    public int SoDonViTrucThuoc { get; set; }
    public int SoDaKetNoiBcanet { get; set; }
    public int SoDuongTruyenVnpt { get; set; }
    public int SoDuongTruyenKhac { get; set; }
    public int SoKetNoiInternet { get; set; }
    public string? GhiChu { get; set; }
}
