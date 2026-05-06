namespace ThucLuc.Application.Features.VanBanQppl;

public sealed class GetVanBanQpplQuery
{
    public long? DonViId { get; set; }
    public string? KyBaoCaoCode { get; set; }
}

public sealed class VanBanQpplDto
{
    public long Id { get; set; }
    public long DonViId { get; set; }
    public string? KyBaoCaoCode { get; set; }
    public string SoHieu { get; set; } = string.Empty;
    public string? TenVanBan { get; set; }
    public string? LoaiVanBan { get; set; }
    public string? CoQuanBanHanh { get; set; }
    public DateOnly NgayBanHanh { get; set; }
    public DateOnly? NgayHieuLuc { get; set; }
    public string? LinhVuc { get; set; }
    public string? TrichYeu { get; set; }
    public string? TinhTrangTrienKhai { get; set; }
    public string? GhiChu { get; set; }
}

public sealed class UpsertVanBanQpplRequest
{
    public long DonViId { get; set; }
    public string SoHieu { get; set; } = string.Empty;
    public string? TenVanBan { get; set; }
    public string? LoaiVanBan { get; set; }
    public string? CoQuanBanHanh { get; set; }
    public DateOnly NgayBanHanh { get; set; }
    public DateOnly? NgayHieuLuc { get; set; }
    public string? LinhVuc { get; set; }
    public string? TrichYeu { get; set; }
    public string? TinhTrangTrienKhai { get; set; }
    public string? GhiChu { get; set; }
}
