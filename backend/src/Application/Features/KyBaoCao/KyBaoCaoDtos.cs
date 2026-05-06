using ThucLuc.Domain.Enums;

namespace ThucLuc.Application.Features.KyBaoCao;

public sealed class KyBaoCaoDto
{
    public long Id { get; set; }

    public string KyCode { get; set; } = string.Empty;

    public long? MauBaoCaoId { get; set; }

    public int Nam { get; set; }

    public int? Quy { get; set; }

    public int? Thang { get; set; }

    public KyBaoCaoStatus TrangThai { get; set; }

    public DateOnly? NgayBatDau { get; set; }

    public DateOnly? NgayKetThuc { get; set; }

    public string? GhiChu { get; set; }
}

public sealed class KyBaoCaoTienDoDto
{
    public long KyBaoCaoId { get; set; }

    public string KyCode { get; set; } = string.Empty;

    public int TongDonVi { get; set; }

    public int SoDonViChuaNhap { get; set; }

    public int SoDonViDangNhap { get; set; }

    public int SoDonViDaXacNhan { get; set; }

    public int SoDonViDangBoSung { get; set; }

    public int SoDonViDaNop { get; set; }

    public IReadOnlyCollection<KyBaoCaoTienDoDonViDto> DonVis { get; set; } = Array.Empty<KyBaoCaoTienDoDonViDto>();
}

public sealed class KyBaoCaoTienDoDonViDto
{
    public long DonViId { get; set; }

    public string TenDonVi { get; set; } = string.Empty;

    public KyBaoCaoStatus KyTrangThai { get; set; }

    public Domain.Enums.KyTrangThaiDonViStatus TrangThaiDonVi { get; set; }

    public long? SnapshotId { get; set; }

    public int? SnapshotPhienBan { get; set; }

    public Domain.Enums.SnapshotStatus? SnapshotTrangThai { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public DateTime? LockedAt { get; set; }

    public DateTime? NgayMoLai { get; set; }

    public DateTime? NgayXacNhan { get; set; }
}

public sealed class CreateKyBaoCaoRequest
{
    public long MauBaoCaoId { get; set; }

    public int Nam { get; set; }

    public int? Quy { get; set; }

    public int? Thang { get; set; }

    public DateOnly? NgayBatDau { get; set; }

    public DateOnly? NgayKetThuc { get; set; }

    public string? GhiChu { get; set; }
}

public sealed class UpdateKyBaoCaoStatusRequest
{
    public KyBaoCaoStatus TrangThai { get; set; }

    public string? GhiChu { get; set; }
}