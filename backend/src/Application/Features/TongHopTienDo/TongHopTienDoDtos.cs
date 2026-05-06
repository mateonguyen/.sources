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

    public DateTime? CapNhatLanCuoi { get; set; }

    // Số bản ghi live từ BIZ_* tại thời điểm query
    public int SoNhanLuc { get; set; }
    public int SoThietBi { get; set; }
    public int SoHeThongThongTin { get; set; }
    public int SoHaTangMang { get; set; }
    public int SoDaoTao { get; set; }
    public int SoDuAn { get; set; }
}

/// <summary>PHONG/XA bật/tắt flag "Đã khai báo xong" (không lock BIZ_*).</summary>
public sealed class XacNhanRequest
{
    public string KyBaoCaoCode { get; set; } = string.Empty;
    public bool DaXacNhan { get; set; }
}
