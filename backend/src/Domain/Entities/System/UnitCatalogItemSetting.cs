using ThucLuc.Domain.Common.Base;

namespace ThucLuc.Domain.Entities.System;

/// <summary>
/// Cấu hình áp dụng một mục danh mục chuẩn tại một đơn vị.
/// CatalogKey là khóa mở (ví dụ CODE:GIAI_PHAP_ATTT, REF:LOAI_THIET_BI),
/// nhờ đó có thể bổ sung loại danh mục mới mà không đổi cấu trúc bảng.
/// </summary>
public sealed class UnitCatalogItemSetting : AuditableEntityBase
{
    public long DonViId { get; set; }

    public string CatalogKey { get; set; } = string.Empty;

    public long ItemId { get; set; }

    public bool IsVisible { get; set; } = true;
}
