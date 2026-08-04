using ThucLuc.Domain.Common.Base;
using ThucLuc.Domain.Enums;

namespace ThucLuc.Domain.Entities.Reporting;

public sealed class KyBaoCao : AuditableEntityBase
{
    /// <summary>
    /// Mã định danh duy nhất của kỳ, tự động sinh từ MauBaoCao.MaMau.
    /// Format: {NAM}Q{Q}_{MA_MAU} | {NAM}T{MM}_{MA_MAU} | {NAM}_{MA_MAU}
    /// Ví dụ: "2025Q1_NHAN_LUC", "2025T06_ATTT", "2025_CAMERA"
    /// </summary>
    public string KyCode { get; set; } = string.Empty;

    public int Nam { get; set; }

    /// <summary>Quý báo cáo (1–4). Null nếu TanSuat = Thang hoặc Nam.</summary>
    public int? Quy { get; set; }

    /// <summary>Tháng báo cáo (1–12). Null nếu TanSuat = Quy hoặc Nam.</summary>
    public int? Thang { get; set; }

    public KyBaoCaoStatus TrangThai { get; set; } = KyBaoCaoStatus.ChuanBi;

    public DateOnly? NgayBatDau { get; set; }

    public DateOnly? NgayKetThuc { get; set; }

    public string? GhiChu { get; set; }

    /// <summary>Tên kỳ do người dùng đặt, phải duy nhất. VD: "Báo cáo An toàn thông tin Quý 2 Năm 2026"</summary>
    public string? TenKy { get; set; }

    /// <summary>FK đến mẫu báo cáo. Xác định loại báo cáo và danh sách module cần nộp.</summary>
    public long? MauBaoCaoId { get; set; }

    public MauBaoCao? MauBaoCao { get; set; }

    public ICollection<KyTrangThaiDonVi> TrangThaiDonVis { get; set; } = new List<KyTrangThaiDonVi>();

    public ICollection<BaoCaoSnapshot> Snapshots { get; set; } = new List<BaoCaoSnapshot>();
}