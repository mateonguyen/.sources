namespace ThucLuc.Application.Features.DuAnCntt;

public sealed class GetDuAnCnttQuery
{
    public long? DonViId { get; set; }
    public string? KyBaoCaoCode { get; set; }
}

public sealed class DuAnCnttDto
{
    public long Id { get; set; }

    public long DonViId { get; set; }

    public string? KyBaoCaoCode { get; set; }

    public string TenDuAn { get; set; } = string.Empty;

    public string? DonViChuTri { get; set; }

    public int? NamTrienKhai { get; set; }

    public int? NamDuaVaoSuDung { get; set; }

    public decimal? TongKinhPhi { get; set; }

    public string? NguonVon { get; set; }

    public string? GhiChu { get; set; }
}

public sealed class UpsertDuAnCnttRequest
{
    public long DonViId { get; set; }

    public string TenDuAn { get; set; } = string.Empty;

    public string? DonViChuTri { get; set; }

    public int? NamTrienKhai { get; set; }

    public int? NamDuaVaoSuDung { get; set; }

    public decimal? TongKinhPhi { get; set; }

    public string? NguonVon { get; set; }

    public string? GhiChu { get; set; }
}