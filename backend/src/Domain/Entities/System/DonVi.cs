using ThucLuc.Domain.Common.Base;

namespace ThucLuc.Domain.Entities.System;

public sealed class DonVi : AuditableSoftDeleteEntityBase
{
    public string MaDonVi { get; set; } = string.Empty;

    public string TenDonVi { get; set; } = string.Empty;

    public string? TenVietTat { get; set; }

    public long? ParentId { get; set; }

    public string? DiaChi { get; set; }

    public bool IsActive { get; set; } = true;

    public string? CapDonVi { get; set; }

    /// <summary>
    /// Cấp đơn vị dạng số (0 = gốc/Bộ, 1 = trực thuộc Bộ, 2 = trực thuộc cấp 1),
    /// suy ra từ <see cref="CapDonVi"/> qua <see cref="ResolveCapSo"/>. Dùng cho mọi
    /// so sánh/filter theo cấp thay vì so chuỗi CapDonVi trực tiếp (chuỗi có nhiều
    /// biến thể lịch sử: CAP_1 hiện hành, TINH/CUC là alias cũ còn sót ở dữ liệu cũ).
    /// </summary>
    public int? CapSo { get; set; }

    public string? KhoiDonVi { get; set; }

    public string? WebsiteNoiBo { get; set; }

    public string? WebsiteInternet { get; set; }

    public int? TongBienChe { get; set; }

    public string CheDoNhapLieu { get; set; } = "TU_NHAP";

    public DonVi? Parent { get; set; }

    public ICollection<DonVi> Children { get; set; } = new List<DonVi>();

    /// <summary>Ánh xạ chuỗi CapDonVi (kể cả alias cũ TINH/CUC) sang cấp số chính thức.</summary>
    public static int? ResolveCapSo(string? capDonVi) => capDonVi?.Trim().ToUpperInvariant() switch
    {
        "CAP_0" => 0,
        "CAP_1" or "TINH" or "CUC" => 1,
        "CAP_2" => 2,
        _ => null,
    };
}