using ThucLuc.Domain.Common.Base;

namespace ThucLuc.Domain.Entities.System;

public sealed class Code : AuditableEntityBase
{
    public string CodeKey { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<CodeValue> Values { get; set; } = new List<CodeValue>();
}
