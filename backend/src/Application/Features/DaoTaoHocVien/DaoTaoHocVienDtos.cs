namespace ThucLuc.Application.Features.DaoTaoHocVien;

public sealed class DaoTaoHocVienQuery
{
    public long? DonViId { get; set; }
    public int? Nam { get; set; }
    public string? KyBaoCaoCode { get; set; }
}

public sealed class DaoTaoHocVienDto
{
    public long Id { get; set; }
    public long DonViId { get; set; }
    public int Nam { get; set; }
    public string NoiDungDaoTao { get; set; } = string.Empty;
    public int SoTienSi { get; set; }
    public int SoThacSi { get; set; }
    public int SoDaiHoc { get; set; }
    public int SoCaoDang { get; set; }
    public int SoTrungCap { get; set; }
    public string? KyBaoCaoCode { get; set; }
    public bool IsLatest { get; set; }
    public int SnapshotVersion { get; set; }
    public string? GhiChu { get; set; }
}

public sealed class UpsertDaoTaoHocVienRequest
{
    public long DonViId { get; set; }
    public int Nam { get; set; }
    public string NoiDungDaoTao { get; set; } = string.Empty;
    public int SoTienSi { get; set; }
    public int SoThacSi { get; set; }
    public int SoDaiHoc { get; set; }
    public int SoCaoDang { get; set; }
    public int SoTrungCap { get; set; }
    public string? GhiChu { get; set; }
}

public sealed class SaveDaoTaoHocVienMatrixRequest
{
    public long DonViId { get; set; }
    public IReadOnlyCollection<UpsertDaoTaoHocVienRequest> Items { get; set; } = Array.Empty<UpsertDaoTaoHocVienRequest>();
}

public sealed class FinalizeDaoTaoHocVienRequest
{
    public long DonViId { get; set; }
    public string KyBaoCaoCode { get; set; } = string.Empty;
}

public sealed class FinalizeDaoTaoHocVienResult
{
    public long BatchId { get; set; }
    public long DonViId { get; set; }
    public string KyBaoCaoCode { get; set; } = string.Empty;
    public DateTime FinishedAt { get; set; }
}
