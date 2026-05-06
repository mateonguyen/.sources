namespace ThucLuc.Application.Common.Contracts;

public interface IDateTimeProvider
{
    DateTime Now { get; }
}