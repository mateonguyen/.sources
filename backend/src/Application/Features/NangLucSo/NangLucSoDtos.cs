namespace ThucLuc.Application.Features.NangLucSo;

public sealed class NangLucSoQuery
{
    public long? DonViId { get; set; }
    public string? KyBaoCaoCode { get; set; }
}

public sealed class NangLucSoDto
{
    public long Id { get; set; }
    public long DonViId { get; set; }
    public string NhomViTri { get; set; } = string.Empty;
    public int TongSoDienDanhGia { get; set; }
    public int TongSoDat { get; set; }
    public int TongSoChuaDat { get; set; }
    public string? KyBaoCaoCode { get; set; }
    public bool IsLatest { get; set; }
    public int SnapshotVersion { get; set; }
    public string? GhiChu { get; set; }
}

public sealed class UpsertNangLucSoRequest
{
    public long DonViId { get; set; }
    public string NhomViTri { get; set; } = string.Empty;
    public int TongSoDienDanhGia { get; set; }
    public int TongSoDat { get; set; }
    public int TongSoChuaDat { get; set; }
    public string? GhiChu { get; set; }
}

public sealed class SaveNangLucSoMatrixRequest
{
    public long DonViId { get; set; }
    public IReadOnlyCollection<UpsertNangLucSoRequest> Items { get; set; } = Array.Empty<UpsertNangLucSoRequest>();
}

public sealed class FinalizeNangLucSoRequest
{
    public long DonViId { get; set; }
    public string KyBaoCaoCode { get; set; } = string.Empty;
}

public sealed class FinalizeNangLucSoResult
{
    public long BatchId { get; set; }
    public long DonViId { get; set; }
    public string KyBaoCaoCode { get; set; } = string.Empty;
    public DateTime FinishedAt { get; set; }
}
