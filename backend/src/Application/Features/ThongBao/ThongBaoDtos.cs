using ThucLuc.Domain.Enums;

namespace ThucLuc.Application.Features.ThongBao;

public sealed class ThongBaoDto
{
    public long Id { get; set; }

    public NotificationType LoaiThongBao { get; set; }

    public string TieuDe { get; set; } = string.Empty;

    public string NoiDung { get; set; } = string.Empty;

    public bool DaDoc { get; set; }

    public DateTime CreatedAt { get; set; }
}