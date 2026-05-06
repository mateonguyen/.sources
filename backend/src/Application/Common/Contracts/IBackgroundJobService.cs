using System.Linq.Expressions;

namespace ThucLuc.Application.Common.Contracts;

public interface IBackgroundJobService
{
    string Enqueue(Expression<Action> methodCall);

    string Enqueue<T>(Expression<Action<T>> methodCall);
}