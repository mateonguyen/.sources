namespace ThucLuc.Domain.Common.Interfaces;

public interface ISoftDelete
{
    DateTime? DeletedAt { get; set; }

    bool IsDeleted => DeletedAt.HasValue;
}