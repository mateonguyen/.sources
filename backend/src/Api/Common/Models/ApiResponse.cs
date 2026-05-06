namespace ThucLuc.Api.Common.Models;

public sealed class ApiResponse<T>
{
    public bool Success { get; init; }

    public T? Data { get; init; }

    public ApiError? Error { get; init; }

    public object? Meta { get; init; }

    public static ApiResponse<T> Ok(T? data, object? meta = null) => new() { Success = true, Data = data, Meta = meta };

    public static ApiResponse<T> Fail(string code, string message, object? meta = null) => new()
    {
        Success = false,
        Error = new ApiError { Code = code, Message = message },
        Meta = meta
    };
}

public sealed class ApiError
{
    public string Code { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;
}