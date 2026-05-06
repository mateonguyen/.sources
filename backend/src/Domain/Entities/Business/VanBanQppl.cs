using ThucLuc.Domain.Common.Base;

namespace ThucLuc.Domain.Entities.Business;

public sealed class VanBanQppl : AuditableSoftDeleteEntityBase
{
    public long DonViId { get; set; }
    public string SoHieu { get; set; } = string.Empty;
    public string? TenVanBan { get; set; }
    public string? LoaiVanBan { get; set; }
    public string? CoQuanBanHanh { get; set; }
    public DateOnly NgayBanHanh { get; set; }
    public DateOnly? NgayHieuLuc { get; set; }
    public string? LinhVuc { get; set; }
    public string? TrichYeu { get; set; }
    public string? TinhTrangTrienKhai { get; set; }
    public string? GhiChu { get; set; }
}
