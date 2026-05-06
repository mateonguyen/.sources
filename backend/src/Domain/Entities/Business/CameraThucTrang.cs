using ThucLuc.Domain.Common.Base;

namespace ThucLuc.Domain.Entities.Business;

public sealed class CameraThucTrang : AuditableSoftDeleteEntityBase
{
    public long DonViId { get; set; }
    public string? NhomCamera { get; set; }
    public string TenHeThong { get; set; } = string.Empty;
    public int CauHinhIp { get; set; }
    public int CauHinhAnalog { get; set; }
    public int ThucTrangIp { get; set; }
    public int ThucTrangAnalog { get; set; }
    public string? ChuDauTu { get; set; }
    public int? NamDauTu { get; set; }
    public string? DuongTruyen { get; set; }
    public string? PhanMem { get; set; }
    public string? LuuTru { get; set; }
    public string? GhiChu { get; set; }
}
