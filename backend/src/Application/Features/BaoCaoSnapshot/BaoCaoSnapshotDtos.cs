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
    public string KyCode { get; set; } = string.Empty;
    public long DonViId { get; set; }
    public string TenDonVi { get; set; } = string.Empty;
    public SnapshotStatus TrangThai { get; set; }
    public int PhienBan { get; set; }
    public string? GhiChu { get; set; }
    public string? SnapshotJson { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? LockedAt { get; set; }
}

public sealed class SnapshotBreakdownUnitDto
{
    public long DonViId { get; set; }

    public string TenDonVi { get; set; } = string.Empty;

    public bool DaXacNhan { get; set; }

    public IReadOnlyCollection<ModuleStatusDto> ModuleCounts { get; set; } = [];
}

public sealed class SnapshotBreakdownDto
{
    public long SnapshotId { get; set; }

    public long KyBaoCaoId { get; set; }

    public string KyCode { get; set; } = string.Empty;

    public long DonViId { get; set; }

    public string TenDonVi { get; set; } = string.Empty;

    public DateTime? SubmittedAt { get; set; }

    public int TotalChildren { get; set; }

    public int ConfirmedChildren { get; set; }

    public IReadOnlyCollection<SnapshotBreakdownUnitDto> Children { get; set; } = [];
}

public sealed class CreateBaoCaoSnapshotRequest
{
    public long KyBaoCaoId { get; set; }
    public long DonViId { get; set; }
    public string? SnapshotJson { get; set; }
    public string? SummaryJson { get; set; }
    public string? GhiChu { get; set; }
}

public sealed class UpdateBaoCaoSnapshotRequest
{
    public string? SnapshotJson { get; set; }
    public string? SummaryJson { get; set; }
    public string? GhiChu { get; set; }
}

public sealed class SubmitCurrentSnapshotRequest
{
    public long KyBaoCaoId { get; set; }
    public long DonViId { get; set; }
    public string? GhiChu { get; set; }
    public bool ForceSubmitWhenChildrenUnconfirmed { get; set; }
}

public sealed class SubmitSnapshotContextDto
{
    public string CheDoNhapLieu { get; set; } = "TU_NHAP";

    public bool IsTongHop { get; set; }

    public int TotalChildren { get; set; }

    public int ConfirmedChildren { get; set; }

    public bool HasUnconfirmedChildren { get; set; }

    public bool HasChildDataChangedAfterLastSubmit { get; set; }

    public DateTime? LastSubmittedAt { get; set; }

    public DateTime? LatestChildUpdatedAt { get; set; }
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

public sealed class ModuleStatusDto
{
    public string ModuleCode { get; set; } = string.Empty;

    /// <summary>Số bản ghi sẽ vào báo cáo (TONG_HOP = chỉ đơn vị cấp dưới).</summary>
    public int RecordCount { get; set; }

    /// <summary>Dữ liệu tự nhập của chính đơn vị. Với TONG_HOP: KHÔNG được tính vào báo cáo (chỉ để cảnh báo).</summary>
    public int OwnRecordCount { get; set; }

    /// <summary>Số bản ghi gộp từ đơn vị cấp dưới (bằng RecordCount khi TONG_HOP).</summary>
    public int ChildRecordCount { get; set; }
}

public sealed class SnapshotModuleCompareItemDto
{
    public string ModuleCode { get; set; } = string.Empty;
    public int FromCount { get; set; }
    public int ToCount { get; set; }
    public int Delta { get; set; }
}

public sealed class SnapshotCompareDto
{
    public long DonViId { get; set; }
    public string TenDonVi { get; set; } = string.Empty;

    public long FromKyBaoCaoId { get; set; }
    public string FromKyCode { get; set; } = string.Empty;
    public long FromSnapshotId { get; set; }

    public long ToKyBaoCaoId { get; set; }
    public string ToKyCode { get; set; } = string.Empty;
    public long ToSnapshotId { get; set; }

    public IReadOnlyCollection<SnapshotModuleCompareItemDto> Modules { get; set; } = [];
}
