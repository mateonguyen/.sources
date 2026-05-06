namespace ThucLuc.Application.Features.HeThongThongTin;

public sealed class HeThongThongTinDto
{
    public long Id { get; set; }
    public long DonViId { get; set; }
    public string LoaiPhanMem { get; set; } = string.Empty;
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
}

public sealed class UpsertHeThongThongTinRequest
{
    public long DonViId { get; set; }
    public string LoaiPhanMem { get; set; } = string.Empty;
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
}