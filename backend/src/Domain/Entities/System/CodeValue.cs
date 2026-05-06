using ThucLuc.Domain.Common.Base;

namespace ThucLuc.Domain.Entities.System;

public sealed class CodeValue : AuditableEntityBase
{
    public long CodeId { get; set; }

    public string Value { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public Code? Code { get; set; }
}
