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

    public string? KhoiDonVi { get; set; }

    public string? WebsiteNoiBo { get; set; }

    public string? WebsiteInternet { get; set; }

    public int? TongBienChe { get; set; }

    public string CheDoNhapLieu { get; set; } = "TU_NHAP";

    public DonVi? Parent { get; set; }

    public ICollection<DonVi> Children { get; set; } = new List<DonVi>();
}