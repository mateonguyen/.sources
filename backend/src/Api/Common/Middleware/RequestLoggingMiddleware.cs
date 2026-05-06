using ThucLuc.Application.Common.Contracts;

namespace ThucLuc.Api.Common.Middleware;

public sealed class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;
    private readonly IDateTimeProvider _dateTimeProvider;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger, IDateTimeProvider dateTimeProvider)
    {
        _next = next;
        _logger = logger;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var startedAt = _dateTimeProvider.Now;
        await _next(context);
        var elapsed = _dateTimeProvider.Now - startedAt;
        var userId = context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous";

        _logger.LogInformation(
            "HTTP {Method} {Path} => {StatusCode} in {ElapsedMs} ms CorrelationId={CorrelationId} UserId={UserId}",
            context.Request.Method,
            context.Request.Path,
            context.Response.StatusCode,
            elapsed.TotalMilliseconds,
            context.TraceIdentifier,
            userId);
    }
}