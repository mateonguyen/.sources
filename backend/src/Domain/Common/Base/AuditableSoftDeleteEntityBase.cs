using ThucLuc.Domain.Common.Interfaces;

namespace ThucLuc.Domain.Common.Base;

public abstract class AuditableSoftDeleteEntityBase : AuditableEntityBase, ISoftDelete
{
    public DateTime? DeletedAt { get; set; }
}