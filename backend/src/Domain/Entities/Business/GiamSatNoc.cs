using ThucLuc.Domain.Common.Base;

namespace ThucLuc.Domain.Entities.Business;

public sealed class GiamSatNoc : AuditableSoftDeleteEntityBase
{
    public long DonViId { get; set; }
    public string LopGiamSat { get; set; } = null!;
    public bool CoNoc { get; set; }
    public string? ThucTrang { get; set; }
    public int TongSoDoiTuong { get; set; }
    public int SoDaGiamSat { get; set; }
    public string? GhiChu { get; set; }
}
