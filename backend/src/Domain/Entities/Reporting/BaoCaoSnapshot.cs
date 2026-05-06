using ThucLuc.Domain.Common.Base;
using ThucLuc.Domain.Entities.System;
using ThucLuc.Domain.Enums;

namespace ThucLuc.Domain.Entities.Reporting;

public sealed class BaoCaoSnapshot : AuditableSoftDeleteEntityBase
{
    public long KyBaoCaoId { get; set; }

    public long DonViId { get; set; }

    public SnapshotStatus TrangThai { get; set; } = SnapshotStatus.Draft;

    public int PhienBan { get; set; } = 1;

    public DateTime? SubmittedAt { get; set; }

    public long? SubmittedBy { get; set; }

    public DateTime? LockedAt { get; set; }

    public long? LockedBy { get; set; }

    public string? GhiChu { get; set; }

    public KyBaoCao? KyBaoCao { get; set; }

    public DonVi? DonVi { get; set; }

    public ICollection<BaoCaoFile> Files { get; set; } = new List<BaoCaoFile>();
}