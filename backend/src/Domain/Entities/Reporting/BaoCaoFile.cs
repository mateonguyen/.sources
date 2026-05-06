using ThucLuc.Domain.Common.Base;

namespace ThucLuc.Domain.Entities.Reporting;

public sealed class BaoCaoFile : AuditableEntityBase
{
    public long BaoCaoSnapshotId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public string MimeType { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public string LoaiFile { get; set; } = "pdf";

    public BaoCaoSnapshot? BaoCaoSnapshot { get; set; }
}