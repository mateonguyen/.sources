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
    public int TongSoDoiTuong { get; set; }
    public int SoGiamSatMotPhan { get; set; }
    public int SoGiamSatCoBan { get; set; }
    public int SoGiamSatDayDu { get; set; }
    public int SoSuCo { get; set; }
    public int SoSuCoDaKhacPhuc { get; set; }
    public string? LucLuongUngCuu { get; set; }
    public string? GhiChu { get; set; }
}

public sealed class UpsertGiamSatSocRequest
{
    public long DonViId { get; set; }
    public string LoaiMang { get; set; } = null!;
    public string LopGiamSat { get; set; } = null!;
    public bool CoHeThong { get; set; }
    public string? ThucTrang { get; set; }
    public int TongSoDoiTuong { get; set; }
    public int SoGiamSatMotPhan { get; set; }
    public int SoGiamSatCoBan { get; set; }
    public int SoGiamSatDayDu { get; set; }
    public int SoSuCo { get; set; }
    public int SoSuCoDaKhacPhuc { get; set; }
    public string? LucLuongUngCuu { get; set; }
    public string? GhiChu { get; set; }
}

public sealed class SaveGiamSatSocMatrixRequest
{
    public long DonViId { get; set; }
    public List<UpsertGiamSatSocRequest> Items { get; set; } = [];
}
