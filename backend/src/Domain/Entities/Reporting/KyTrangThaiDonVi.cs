using ThucLuc.Domain.Common.Base;
using ThucLuc.Domain.Entities.System;
using ThucLuc.Domain.Enums;

namespace ThucLuc.Domain.Entities.Reporting;

public sealed class KyTrangThaiDonVi : AuditableEntityBase
{
    public long KyBaoCaoId { get; set; }

    public long DonViId { get; set; }

    public KyTrangThaiDonViStatus TrangThai { get; set; } = KyTrangThaiDonViStatus.ChuaNhap;

    public DateTime? NgayXacNhan { get; set; }

    public long? ConfirmedBy { get; set; }

    public DateTime? NgayMoLai { get; set; }

    public long? MoLaiBy { get; set; }

    /// <summary>Flag khai báo của PHONG/XA: "Tôi đã nhập xong." Không lock BIZ_*.</summary>
    public bool DaXacNhan { get; set; } = false;

    public KyBaoCao? KyBaoCao { get; set; }

    public DonVi? DonVi { get; set; }
}