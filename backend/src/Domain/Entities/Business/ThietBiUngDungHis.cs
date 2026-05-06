using ThucLuc.Domain.Common.Base;

namespace ThucLuc.Domain.Entities.Business;

public sealed class ThietBiUngDungHis : AuditableEntityBase
{
    public long SourceThietBiId { get; set; }

    public long SourceHeThongId { get; set; }

    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
    public int VersionNo { get; set; }
}
