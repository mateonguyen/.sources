using Microsoft.AspNetCore.Mvc.Filters;
using ThucLuc.Application.Common.Contracts;
using ThucLuc.Domain.Enums;

namespace ThucLuc.Api.Common.Filters;

public sealed class AuditActionFilter : IAsyncActionFilter
{
    private readonly IAuditLogService _auditLogService;

    public AuditActionFilter(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();
        if (executed.Exception is not null || context.HttpContext.Response.StatusCode >= 400)
        {
            return;
        }

        var actionType = context.HttpContext.Request.Method.ToUpperInvariant() switch
        {
            "POST" => AuditActionType.Create,
            "PUT" => AuditActionType.Update,
            "PATCH" => AuditActionType.Update,
            "DELETE" => AuditActionType.Delete,
            _ => (AuditActionType?)null
        };

        if (!actionType.HasValue)
        {
            return;
        }

        long? recordId = context.RouteData.Values.TryGetValue("id", out var idValue) && long.TryParse(idValue?.ToString(), out var parsedId)
            ? parsedId
            : null;

        await _auditLogService.WriteAsync(
            actionType.Value,
            context.ActionDescriptor.RouteValues["controller"] ?? "Unknown",
            recordId,
            null,
            null,
            context.HttpContext.Request.Path,
            context.HttpContext.Connection.RemoteIpAddress?.ToString(),
            context.HttpContext.Request.Headers.UserAgent.ToString(),
            context.HttpContext.RequestAborted);
    }
}