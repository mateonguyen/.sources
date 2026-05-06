using ThucLuc.Domain.Common.Base;
using ThucLuc.Domain.Enums;

namespace ThucLuc.Domain.Entities.System;

public sealed class YeuCauBoSung : EntityBase
{
    public long KyBaoCaoId { get; set; }

    public long DonViId { get; set; }

    public YeuCauBoSungStatus TrangThai { get; set; } = YeuCauBoSungStatus.ChoDuyet;

    public string LyDo { get; set; } = string.Empty;

    public long RequestedBy { get; set; }

    public DateTime RequestedAt { get; set; }

    public long? ApprovedBy { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public string? TuChoiLyDo { get; set; }

    public DateOnly? HanBoSung { get; set; }

    public DateTime? CompletedAt { get; set; }

    // "BO_XUONG_TINH" (H05→TINH, hiện tại) | "TINH_XUONG_PHONG" (TINH→PHONG, mới)
    public string CapGui { get; set; } = "BO_XUONG_TINH";
}