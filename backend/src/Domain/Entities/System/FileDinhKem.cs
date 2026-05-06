using ThucLuc.Domain.Common.Base;

namespace ThucLuc.Domain.Entities.System;

public sealed class FileDinhKem : AuditableSoftDeleteEntityBase
{
    public long DonViId { get; set; }

    public string EntityType { get; set; } = string.Empty;

    public long EntityId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public string MimeType { get; set; } = string.Empty;

    public long UploadedBy { get; set; }

    public DateTime UploadedAt { get; set; }
}