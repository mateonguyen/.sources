using ThucLuc.Domain.Common.Base;

namespace ThucLuc.Domain.Entities.System;

public sealed class RefLoaiThietBi : AuditableEntityBase
{
    public long? ParentId { get; set; }
    public string MaLoai { get; set; } = string.Empty;
    public string TenLoai { get; set; } = string.Empty;
    public int Cap { get; set; }
    public bool LaTongHop { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public RefLoaiThietBi? Parent { get; set; }
    public ICollection<RefLoaiThietBi> Children { get; set; } = new List<RefLoaiThietBi>();
}