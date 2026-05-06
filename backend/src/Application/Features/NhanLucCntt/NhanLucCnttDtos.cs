namespace ThucLuc.Application.Features.NhanLucCntt;

public sealed class NhanLucCnttQuery
{
    public string? TuKhoa { get; set; }

    public int? NamBaoCao { get; set; }

    public DateTime? AsOfDate { get; set; }

    public long? DonViCongTacId { get; set; }

    public string? GioiTinh { get; set; }

    public string? CapBac { get; set; }

    public string? LoaiNhanLuc { get; set; }

    public string? TrinhDoCntt { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 10;
}

public sealed class NhanLucCnttDto
{
    public long Id { get; set; }

    public string NhanSuKey { get; set; } = string.Empty;

    public long DonViId { get; set; }

    public string? DonViTen { get; set; }

    public long? DonViCongTacId { get; set; }

    public string? DonViCongTacTen { get; set; }

    public string HoTen { get; set; } = string.Empty;

    public DateOnly? NgaySinh { get; set; }

    public string? GioiTinh { get; set; }

    public string? CapBac { get; set; }

    public string? ChucVu { get; set; }

    public string? DienThoai { get; set; }

    public string? LoaiNhanLuc { get; set; }

    public string? TrinhDoCntt { get; set; }

    public string? TrinhDoLlct { get; set; }

    public string? GhiChu { get; set; }

    public int? NamBaoCao { get; set; }

    public DateTime ValidFrom { get; set; }

    public DateTime? ValidTo { get; set; }

    public bool IsCurrent { get; set; }

    public int VersionNo { get; set; }
}

public sealed class UpsertNhanLucCnttRequest
{
    public long DonViId { get; set; }

    public long? DonViCongTacId { get; set; }

    public string HoTen { get; set; } = string.Empty;

    public DateOnly? NgaySinh { get; set; }

    public string? GioiTinh { get; set; }

    public string? CapBac { get; set; }

    public string? ChucVu { get; set; }

    public string? DienThoai { get; set; }

    public string? LoaiNhanLuc { get; set; }

    public string? TrinhDoCntt { get; set; }

    public string? TrinhDoLlct { get; set; }

    public string? GhiChu { get; set; }
}
