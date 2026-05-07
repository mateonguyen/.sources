namespace ThucLuc.Application.Features.GiamSatNoc;

public sealed class GiamSatNocQuery
{
    public long? DonViId { get; set; }
    public string? LopGiamSat { get; set; }
    public string? KyBaoCaoCode { get; set; }
}

public sealed class GiamSatNocDto
{
    public long Id { get; set; }
    public long DonViId { get; set; }
    public string? KyBaoCaoCode { get; set; }
    public string LopGiamSat { get; set; } = null!;
    public bool CoNoc { get; set; }
    public string? ThucTrang { get; set; }
    public int TongSoDoiTuong { get; set; }
    public int SoDaGiamSat { get; set; }
    public string? GhiChu { get; set; }
}

public sealed class UpsertGiamSatNocRequest
{
    public long DonViId { get; set; }
    public string LopGiamSat { get; set; } = null!;
    public bool CoNoc { get; set; }
    public string? ThucTrang { get; set; }
    public int TongSoDoiTuong { get; set; }
    public int SoDaGiamSat { get; set; }
    public string? GhiChu { get; set; }
}

public sealed class SaveGiamSatNocMatrixRequest
{
    public long DonViId { get; set; }
    public List<UpsertGiamSatNocRequest> Items { get; set; } = [];
}
