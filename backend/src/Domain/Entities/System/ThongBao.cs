using ThucLuc.Domain.Common.Base;
using ThucLuc.Domain.Enums;

namespace ThucLuc.Domain.Entities.System;

public sealed class ThongBao : EntityBase
{
    public long? DonViId { get; set; }

    public long? UserId { get; set; }

    public NotificationType LoaiThongBao { get; set; }

    public string TieuDe { get; set; } = string.Empty;

    public string NoiDung { get; set; } = string.Empty;

    public bool DaDoc { get; set; }

    public DateTime? DocLuc { get; set; }

    public long? LienKyId { get; set; }

    public long? LienYeuCauId { get; set; }

    public DateTime CreatedAt { get; set; }
}