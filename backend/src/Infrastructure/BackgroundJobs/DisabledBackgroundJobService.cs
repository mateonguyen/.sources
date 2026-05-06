using System.Linq.Expressions;
using ThucLuc.Application.Common.Contracts;

namespace ThucLuc.Infrastructure.BackgroundJobs;

public sealed class DisabledBackgroundJobService : IBackgroundJobService
{
    public string Enqueue(Expression<Action> methodCall)
    {
        return $"disabled:{Guid.NewGuid():N}";
    }

    public string Enqueue<T>(Expression<Action<T>> methodCall)
    {
        return $"disabled:{Guid.NewGuid():N}";
    }
}