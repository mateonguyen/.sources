using ThucLuc.Domain.Common.Base;

namespace ThucLuc.Domain.Entities.Business;

public sealed class DuAnCntt : AuditableSoftDeleteEntityBase
{
    public long DonViId { get; set; }
    public string TenDuAn { get; set; } = string.Empty;
    public string? DonViChuTri { get; set; }
    public int? NamTrienKhai { get; set; }
    public int? NamDuaVaoSuDung { get; set; }
    public decimal? TongKinhPhi { get; set; }
    public string? NguonVon { get; set; }
    public string? GhiChu { get; set; }
}
