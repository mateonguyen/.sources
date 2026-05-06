using FluentValidation;
using System.Text.Json;
using ThucLuc.Api.Common.Models;
using ThucLuc.Application.Common.Exceptions;

namespace ThucLuc.Api.Common.Middleware;

public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ValidationException exception)
        {
            await WriteErrorAsync(context, 400, "VALIDATION_ERROR", exception.Message, exception.Errors.Select(x => new { x.PropertyName, x.ErrorMessage }));
        }
        catch (AppException exception)
        {
            await WriteErrorAsync(context, exception.StatusCode, exception.Code, exception.Message, null);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unhandled exception. CorrelationId={CorrelationId}", context.TraceIdentifier);
            await WriteErrorAsync(context, 500, "INTERNAL_SERVER_ERROR", "Đã xảy ra lỗi hệ thống.", null);
        }
    }

    private static async Task WriteErrorAsync(HttpContext context, int statusCode, string code, string message, object? meta)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        context.Response.Headers.CacheControl = "no-store";
        var payload = ApiResponseFactory.Error(code, message, meta);
        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}