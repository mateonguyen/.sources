using ThucLuc.Domain.Common.Base;

namespace ThucLuc.Domain.Entities.Business;

public sealed class NangLucSo : AuditableSoftDeleteEntityBase
{
    public long DonViId { get; set; }
    public string NhomViTri { get; set; } = string.Empty;
    public int TongSoDienDanhGia { get; set; }
    public int TongSoDat { get; set; }
    public int TongSoChuaDat { get; set; }
    public string? GhiChu { get; set; }
}
