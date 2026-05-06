namespace ThucLuc.Api.Common.Models;

public static class ApiResponseFactory
{
    public static ApiResponse<T> Success<T>(T? data, object? meta = null) => ApiResponse<T>.Ok(data, meta);

    public static ApiResponse<object> Error(string code, string message, object? meta = null) => ApiResponse<object>.Fail(code, message, meta);
}