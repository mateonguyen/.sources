namespace ThucLuc.Application.Features.GiamSatSoc;

public sealed class GiamSatSocQuery
{
    public long? DonViId { get; set; }
    public string? LoaiMang { get; set; }
    public string? LopGiamSat { get; set; }
    public string? KyBaoCaoCode { get; set; }
}

public sealed class GiamSatSocDto
{
    public long Id { get; set; }
    public long DonViId { get; set; }
    public string? KyBaoCaoCode { get; set; }
    public string LoaiMang { get; set; } = null!;
    public string LopGiamSat { get; set; } = null!;
    public bool CoHeThong { get; set; }
    public string? ThucTrang { get; set; }
    public int? NamThanhLap { get; set; }
    public int? SoNhanSu { get; set; }
    public string? CongCuSuDung { get; set; }
    public int? SoCanhBaoThang { get; set; }
    public string? GhiChu { get; set; }
}

public sealed class UpsertGiamSatSocRequest
{
    public long DonViId { get; set; }
    public string LoaiMang { get; set; } = null!;
    public string LopGiamSat { get; set; } = null!;
    public bool CoHeThong { get; set; }
    public string? ThucTrang { get; set; }
    public int? NamThanhLap { get; set; }
    public int? SoNhanSu { get; set; }
    public string? CongCuSuDung { get; set; }
    public int? SoCanhBaoThang { get; set; }
    public string? GhiChu { get; set; }
}

public sealed class SaveGiamSatSocMatrixRequest
{
    public long DonViId { get; set; }
    public List<UpsertGiamSatSocRequest> Items { get; set; } = [];
}
