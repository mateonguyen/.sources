namespace ThucLuc.Application.Features.ThietBiCntt;

public sealed class ThietBiCnttDto
{
    public long Id { get; set; }
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
    public List<long> UngDungIds { get; set; } = new();
}

public sealed class UpsertThietBiCnttRequest
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
    public List<long> UngDungIds { get; set; } = new();
}
