namespace ThucLuc.Application.Features.CameraQuanLy;

public sealed class GetCameraQuanLyQuery
{
    public long? DonViId { get; set; }
    public string? NhomCamera { get; set; }
    public string? KyBaoCaoCode { get; set; }
}

public sealed class CameraQuanLyDto
{
    public long Id { get; set; }
    public long DonViId { get; set; }
    public string? KyBaoCaoCode { get; set; }
    public string? NhomCamera { get; set; }
    public string TenDonViDiaChi { get; set; } = string.Empty;
    public int BuongGiamTrangBiSl { get; set; }
    public int BuongGiamTrangBiTs { get; set; }
    public int NhuCauDauTu { get; set; }
    public int BaoTri { get; set; }
    public int SuaChua { get; set; }
    public int SoLanViPham { get; set; }
    public string? KetNoiChiaSe { get; set; }
    public int HoSoCapDoAttt { get; set; }
    public int CbChuyenTrach { get; set; }
    public int CbKiemNhiem { get; set; }
    public int CbDiaPhuong { get; set; }
    public int DaoTaoBo { get; set; }
    public int DaoTaoNhuCau { get; set; }
    public string? GhiChu { get; set; }
}

public sealed class UpsertCameraQuanLyRequest
{
    public long DonViId { get; set; }
    public string? NhomCamera { get; set; }
    public string TenDonViDiaChi { get; set; } = string.Empty;
    public int BuongGiamTrangBiSl { get; set; }
    public int BuongGiamTrangBiTs { get; set; }
    public int NhuCauDauTu { get; set; }
    public int BaoTri { get; set; }
    public int SuaChua { get; set; }
    public int SoLanViPham { get; set; }
    public string? KetNoiChiaSe { get; set; }
    public int HoSoCapDoAttt { get; set; }
    public int CbChuyenTrach { get; set; }
    public int CbKiemNhiem { get; set; }
    public int CbDiaPhuong { get; set; }
    public int DaoTaoBo { get; set; }
    public int DaoTaoNhuCau { get; set; }
    public string? GhiChu { get; set; }
}
