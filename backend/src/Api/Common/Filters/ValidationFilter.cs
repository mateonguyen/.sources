using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ThucLuc.Api.Common.Models;

namespace ThucLuc.Api.Common.Filters;

public sealed class ValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.ModelState.IsValid)
        {
            var errors = context.ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .Select(x => new
                {
                    Field = x.Key,
                    Errors = x.Value!.Errors.Select(error => error.ErrorMessage).ToArray()
                })
                .ToArray();

            context.Result = new BadRequestObjectResult(ApiResponseFactory.Error("VALIDATION_ERROR", "Dữ liệu đầu vào không hợp lệ.", errors));
            return;
        }

        await next();
    }
}