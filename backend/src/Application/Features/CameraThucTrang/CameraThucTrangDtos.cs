namespace ThucLuc.Application.Features.CameraThucTrang;

public sealed class GetCameraThucTrangQuery
{
    public long? DonViId { get; set; }
    public string? NhomCamera { get; set; }
    public string? KyBaoCaoCode { get; set; }
}

public sealed class CameraThucTrangDto
{
    public long Id { get; set; }
    public long DonViId { get; set; }
    public string? KyBaoCaoCode { get; set; }
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

public sealed class UpsertCameraThucTrangRequest
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
