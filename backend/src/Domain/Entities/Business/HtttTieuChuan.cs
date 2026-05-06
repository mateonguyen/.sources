using ThucLuc.Domain.Common.Base;

namespace ThucLuc.Domain.Entities.Business;

public sealed class HtttTieuChuan : AuditableSoftDeleteEntityBase
{
    public long DonViId { get; set; }
    public string TenHeThong { get; set; } = string.Empty;
    public string? Dvt { get; set; }
    public int SoH05 { get; set; }
    public int SoTinh { get; set; }
    public int SoXa { get; set; }
    public int SoDvTrucThuocBo { get; set; }
    public string? GhiChu { get; set; }

    public DateTime ValidFrom { get; set; }
    public int VersionNo { get; set; }
}
