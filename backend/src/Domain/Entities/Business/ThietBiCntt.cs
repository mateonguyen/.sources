using ThucLuc.Domain.Common.Base;
using ThucLuc.Domain.Entities.System;

namespace ThucLuc.Domain.Entities.Business;

public sealed class ThietBiCntt : AuditableSoftDeleteEntityBase
{
    public long DonViId { get; set; }

    public long LoaiThietBiId { get; set; }

    public string? TenThietBi { get; set; }
    public string? HangSanXuat { get; set; }
    public string? Model { get; set; }
    public string? CauHinh { get; set; }
    public string? HeDieuHanh { get; set; }
    public string? DonViSuDung { get; set; }
    public int SoLuongTong { get; set; }
    public int SoLuongHienDung { get; set; }
    public int SoLuongHong { get; set; }
    public string? TinhTrang { get; set; }
    public string? GhiChu { get; set; }

    public DateTime ValidFrom { get; set; }
    public int VersionNo { get; set; }

    public RefLoaiThietBi? LoaiThietBi { get; set; }
    public ICollection<ThietBiUngDung> UngDungs { get; set; } = new List<ThietBiUngDung>();
}
