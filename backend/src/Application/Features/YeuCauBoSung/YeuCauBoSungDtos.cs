using ThucLuc.Domain.Enums;

namespace ThucLuc.Application.Features.YeuCauBoSung;

public sealed class YeuCauBoSungDto
{
    public long Id { get; set; }
    public long KyBaoCaoId { get; set; }
    public long DonViId { get; set; }
    public string TenDonVi { get; set; } = string.Empty;
    public YeuCauBoSungStatus TrangThai { get; set; }
    public string LyDo { get; set; } = string.Empty;
    public long RequestedBy { get; set; }
    public DateTime RequestedAt { get; set; }
    public long? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? TuChoiLyDo { get; set; }
    public DateOnly? HanBoSung { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string CapGui { get; set; } = "BO_XUONG_TINH";
}

public sealed class CreateYeuCauBoSungRequest
{
    public long KyBaoCaoId { get; set; }
    public long DonViId { get; set; }
    public string LyDo { get; set; } = string.Empty;
    public DateOnly? HanBoSung { get; set; }
}

public sealed class DuyetYeuCauRequest
{
    public DateOnly? HanBoSung { get; set; }
}

public sealed class TuChoiYeuCauRequest
{
    public string TuChoiLyDo { get; set; } = string.Empty;
}