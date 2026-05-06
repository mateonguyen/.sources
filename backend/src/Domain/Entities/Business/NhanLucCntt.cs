using ThucLuc.Domain.Common.Base;
using ThucLuc.Domain.Entities.System;

namespace ThucLuc.Domain.Entities.Business;

public sealed class NhanLucCntt : AuditableSoftDeleteEntityBase
{
    public string NhanSuKey { get; set; } = string.Empty;

    public long DonViId { get; set; }

    public DateTime ValidFrom { get; set; }

    public int VersionNo { get; set; } = 1;

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

    public DonVi? DonViCongTac { get; set; }
}