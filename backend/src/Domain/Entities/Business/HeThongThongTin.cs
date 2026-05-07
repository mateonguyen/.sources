using ThucLuc.Domain.Common.Base;

namespace ThucLuc.Domain.Entities.Business;

public sealed class HeThongThongTin : AuditableSoftDeleteEntityBase
{
    public long DonViId { get; set; }

    public string TenPhanMem { get; set; } = string.Empty;

    public string? DonViPhatTrien { get; set; }

    public string? DonViQuanLy { get; set; }

    public int? NamTrienKhai { get; set; }

    public string? PhamViHoatDong { get; set; }

    public string? PhamViHoatDongKyThuat { get; set; }

    public string? UngDungCnMoi { get; set; }

    public string? KhaNangTichHop { get; set; }

    public bool DaCongNhanSangKien { get; set; }

    public string? GhiChu { get; set; }

    public DateTime ValidFrom { get; set; }
    public int VersionNo { get; set; }
}