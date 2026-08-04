using ThucLuc.Domain.Enums;

namespace ThucLuc.Application.Features.TongHopTienDo;

/// <summary>Query để CA tỉnh xem dashboard tiến độ PHONG/XA con (số liệu live từ BIZ_*).</summary>
public sealed class TienDoDonViQuery
{
    public string KyBaoCaoCode { get; set; } = string.Empty;

    /// <summary>null = lấy children của DonViId của user hiện tại.</summary>
    public long? ParentDonViId { get; set; }
}

/// <summary>Tiến độ + số liệu live của một PHONG/XA.</summary>
public sealed class TienDoDonViDto
{
    public long DonViId { get; set; }
    public string TenDonVi { get; set; } = string.Empty;
    public string CapDonVi { get; set; } = string.Empty;

    /// <summary>PHONG/XA đã bật flag "Đã khai báo xong" (không lock dữ liệu).</summary>
    public bool DaXacNhan { get; set; }

    /// <summary>Thời điểm bấm xác nhận gần nhất.</summary>
    public DateTime? NgayXacNhan { get; set; }

    /// <summary>true = có bản ghi BIZ_* thay đổi SAU thời điểm xác nhận → cần xác nhận lại.</summary>
    public bool CoThayDoiSauXacNhan { get; set; }

    public DateTime? CapNhatLanCuoi { get; set; }

    /// <summary>Hạn bổ sung từ YCBS đang DangBoSung (nếu có).</summary>
    public DateOnly? HanBoSung { get; set; }

    // Số bản ghi live từ BIZ_* tại thời điểm query — đầy đủ 17 module
    public int SoNhanLuc { get; set; }
    public int SoNangLucSo { get; set; }
    public int SoDaoTao { get; set; }
    public int SoDaoTaoHocVien { get; set; }
    public int SoHeThongThongTin { get; set; }
    public int SoHtttTieuChuan { get; set; }
    public int SoDuAn { get; set; }
    public int SoThietBi { get; set; }
    public int SoHaTangMang { get; set; }
    public int SoGiamSatNoc { get; set; }
    public int SoCameraQuanLy { get; set; }
    public int SoCameraThucTrang { get; set; }
    public int SoGiamSatSoc { get; set; }
    public int SoAtttVanHanh { get; set; }
    public int SoAtttDauTu { get; set; }
    public int SoAtttGiaiPhap { get; set; }
    public int SoVanBanQppl { get; set; }
}

/// <summary>PHONG/XA bật/tắt flag "Đã khai báo xong" (không lock BIZ_*).</summary>
public sealed class XacNhanRequest
{
    public string KyBaoCaoCode { get; set; } = string.Empty;
    public bool DaXacNhan { get; set; }
}

/// <summary>Cột hiển thị của bảng chi tiết bản ghi.</summary>
public sealed class ChiTietColumnDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

/// <summary>Chi tiết bản ghi live của 1 module thuộc 1 đơn vị con (cấp trên soi trước khi tổng hợp).</summary>
public sealed class ChiTietModuleDto
{
    public long DonViId { get; set; }
    public string TenDonVi { get; set; } = string.Empty;
    public string ModuleCode { get; set; } = string.Empty;
    public int TotalRows { get; set; }
    public IReadOnlyCollection<ChiTietColumnDto> Columns { get; set; } = Array.Empty<ChiTietColumnDto>();
    public IReadOnlyCollection<Dictionary<string, object?>> Rows { get; set; } = Array.Empty<Dictionary<string, object?>>();
}
