using ThucLuc.Domain.Common.Base;
using ThucLuc.Domain.Enums;

namespace ThucLuc.Domain.Entities.Reporting;

public sealed class MauBaoCao : AuditableEntityBase
{
    /// <summary>Mã định danh ngắn, unique — e.g. "NHAN_LUC", "ATTT", "CAMERA".</summary>
    public string MaMau { get; set; } = string.Empty;

    /// <summary>Tên hiển thị cho người dùng — e.g. "Báo cáo Nhân lực CNTT".</summary>
    public string TenMau { get; set; } = string.Empty;

    public string? MoTa { get; set; }

    /// <summary>Tần suất báo cáo: Tháng / Quý / Năm.</summary>
    public TanSuat TanSuat { get; set; } = TanSuat.Quy;

    /// <summary>
    /// JSON array các module key thuộc mẫu này.
    /// Convention: key = tên BIZ table bỏ prefix "BIZ_", e.g. ["NHAN_LUC_CNTT","NANG_LUC_SO"].
    /// Frontend đọc để biết màn hình nào cần hiển thị.
    /// Backend đọc khi build SnapshotJson để biết bảng nào cần query.
    /// </summary>
    public string DanhSachModule { get; set; } = "[]";

    public bool IsActive { get; set; } = true;

    public ICollection<KyBaoCao> KyBaoCaos { get; set; } = new List<KyBaoCao>();
}
