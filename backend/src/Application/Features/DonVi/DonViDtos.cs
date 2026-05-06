namespace ThucLuc.Application.Features.DonVi;

public sealed class DonViDto
{
    public long Id { get; set; }

    public string MaDonVi { get; set; } = string.Empty;

    public string TenDonVi { get; set; } = string.Empty;

    public string? TenVietTat { get; set; }

    public long? ParentId { get; set; }

    public string? ParentDisplayName { get; set; }

    public string? DiaChi { get; set; }

    public string? CapDonVi { get; set; }

    public string? WebsiteNoiBo { get; set; }

    public string? WebsiteInternet { get; set; }

    public int? TongBienChe { get; set; }

    public int SoDonViCapPhong { get; set; }

    public int SoDonViCapXa { get; set; }

    public bool IsActive { get; set; }

    public IReadOnlyCollection<DonViDto> Children { get; set; } = Array.Empty<DonViDto>();
}

public sealed class DonViParentCandidateDto
{
    public long Id { get; set; }

    public string MaDonVi { get; set; } = string.Empty;

    public string TenDonVi { get; set; } = string.Empty;

    public string? TenVietTat { get; set; }

    public int Level { get; set; }

    public string DisplayName { get; set; } = string.Empty;
}

public sealed class UpsertDonViRequest
{
    public string MaDonVi { get; set; } = string.Empty;

    public string TenDonVi { get; set; } = string.Empty;

    public string? TenVietTat { get; set; }

    public long? ParentId { get; set; }

    public string? DiaChi { get; set; }

    public string? CapDonVi { get; set; }

    public string? WebsiteNoiBo { get; set; }

    public string? WebsiteInternet { get; set; }

    public int? TongBienChe { get; set; }

    public bool IsActive { get; set; } = true;
}