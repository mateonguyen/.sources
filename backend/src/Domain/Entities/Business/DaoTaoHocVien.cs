using ThucLuc.Domain.Common.Base;

namespace ThucLuc.Domain.Entities.Business;

public sealed class DaoTaoHocVien : AuditableSoftDeleteEntityBase
{
    public long DonViId { get; set; }
    public int Nam { get; set; }
    public string NoiDungDaoTao { get; set; } = string.Empty;
    public int SoTienSi { get; set; }
    public int SoThacSi { get; set; }
    public int SoDaiHoc { get; set; }
    public int SoCaoDang { get; set; }
    public int SoTrungCap { get; set; }
    public string? GhiChu { get; set; }
}
