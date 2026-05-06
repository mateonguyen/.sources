namespace ThucLuc.Application.Features.DaoTaoBoiDuong;

public sealed class DaoTaoBoiDuongQuery
{
    public string? KyBaoCaoCode { get; set; }
}

public sealed class DaoTaoBoiDuongDto
{
    public long Id { get; set; }
    public long DonViId { get; set; }
    public string? KyBaoCaoCode { get; set; }
    public string TenKhoaHoc { get; set; } = string.Empty;
    public string DonViToChuc { get; set; } = string.Empty;
    public string? HinhThuc { get; set; }
    public int? SoLuongHv { get; set; }
    public DateOnly? ThoiGianTu { get; set; }
    public DateOnly? ThoiGianDen { get; set; }
    public string? GhiChu { get; set; }
}

public sealed class UpsertDaoTaoBoiDuongRequest
{
    public string TenKhoaHoc { get; set; } = string.Empty;
    public string DonViToChuc { get; set; } = string.Empty;
    public string? HinhThuc { get; set; }
    public int? SoLuongHv { get; set; }
    public DateOnly? ThoiGianTu { get; set; }
    public DateOnly? ThoiGianDen { get; set; }
    public string? GhiChu { get; set; }
}

public sealed class FinalizeDaoTaoBoiDuongRequest
{
    public long DonViId { get; set; }
    public string KyBaoCaoCode { get; set; } = string.Empty;
}

public sealed class FinalizeDaoTaoBoiDuongResult
{
    public string SnapshotBatchId { get; set; } = string.Empty;
    public long DonViId { get; set; }
    public string KyBaoCaoCode { get; set; } = string.Empty;
    public int AffectedRows { get; set; }
    public DateTime FinishedAt { get; set; }
}
