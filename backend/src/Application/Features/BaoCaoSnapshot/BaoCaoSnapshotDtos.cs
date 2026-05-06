namespace ThucLuc.Application.Features.BaoCaoSnapshot;

using ThucLuc.Domain.Enums;

public sealed class DaoTaoBoiDuongPreviewItem
{
    public long Id { get; set; }
    public string TenKhoaHoc { get; set; } = string.Empty;
    public string DonViToChuc { get; set; } = string.Empty;
    public string? HinhThuc { get; set; }
    public int? SoLuongHv { get; set; }
    public DateOnly? ThoiGianTu { get; set; }
    public DateOnly? ThoiGianDen { get; set; }
    public string? GhiChu { get; set; }
    // "in_range" | "out_of_range" | "no_date"
    public string Flag { get; set; } = "in_range";
}

public sealed class BaoCaoSnapshotDto
{
    public long Id { get; set; }
    public long KyBaoCaoId { get; set; }
    public long DonViId { get; set; }
    public SnapshotStatus TrangThai { get; set; }
    public int PhienBan { get; set; }
    public string? GhiChu { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? LockedAt { get; set; }
}

public sealed class CreateBaoCaoSnapshotRequest
{
    public long KyBaoCaoId { get; set; }
    public long DonViId { get; set; }
    public string? GhiChu { get; set; }
}

public sealed class UpdateBaoCaoSnapshotRequest
{
    public string? GhiChu { get; set; }
}

public sealed class SubmitCurrentSnapshotRequest
{
    public long KyBaoCaoId { get; set; }
    public long DonViId { get; set; }
    public string? GhiChu { get; set; }
}

public sealed class SubmitBaoCaoSnapshotRequest
{
    public string? GhiChu { get; set; }
}

public sealed class FinalizeBizModuleRequest
{
    public long DonViId { get; set; }
    public string KyBaoCaoCode { get; set; } = string.Empty;
    public string? ModuleKey { get; set; }
}

public sealed class FinalizeBizModuleResult
{
    public long BatchId { get; set; }
    public long DonViId { get; set; }
    public string KyBaoCaoCode { get; set; } = string.Empty;
    public string? ModuleKey { get; set; }
    public DateTime FinishedAt { get; set; }
}

public sealed class BaoCaoPdfResultDto
{
    public long SnapshotId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
}
