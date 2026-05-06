using ThucLuc.Domain.Common.Interfaces;

namespace ThucLuc.Domain.Common.Base;

public abstract class AuditableEntityBase : EntityBase, IAuditableEntity
{
    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public long? UpdatedBy { get; set; }
}