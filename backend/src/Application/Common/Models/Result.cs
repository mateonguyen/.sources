namespace ThucLuc.Application.Common.Models;

public sealed class Result<T>
{
    public bool Succeeded { get; private init; }

    public T? Data { get; private init; }

    public string? ErrorCode { get; private init; }

    public string? ErrorMessage { get; private init; }

    public static Result<T> Success(T data) => new()
    {
        Succeeded = true,
        Data = data
    };

    public static Result<T> Failure(string errorCode, string errorMessage) => new()
    {
        Succeeded = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage
    };
}