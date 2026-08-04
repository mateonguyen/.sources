using ThucLuc.Domain.Common.Base;
using ThucLuc.Domain.Entities.System;

namespace ThucLuc.Domain.Entities.Reporting;

public sealed class BaoCaoSnapshotXacNhan : AuditableEntityBase
{
    public long SnapshotId { get; set; }

    public long DonViId { get; set; }

    public bool DaXacNhan { get; set; }

    public DateTime? XacNhanAt { get; set; }

    public BaoCaoSnapshot? Snapshot { get; set; }

    public DonVi? DonVi { get; set; }
}