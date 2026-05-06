using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThucLuc.Api.Common.Authorization;
using ThucLuc.Api.Common.Models;
using ThucLuc.Application.Common.Contracts;
using ThucLuc.Application.Security;

namespace ThucLuc.Api.Controllers.V1;

public sealed class PermissionItemDto
{
    public long Id { get; set; }

    public string PermCode { get; set; } = string.Empty;

    public string Module { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string? MoTa { get; set; }
}

[ApiController]
[Route("api/v1/permissions")]
[ApiExplorerSettings(GroupName = "auth")]
public sealed class PermissionsController : ControllerBase
{
    private readonly IApplicationDbContext _dbContext;

    public PermissionsController(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    [HasPermission(Permissions.Roles.Read)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<PermissionItemDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<PermissionItemDto>>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _dbContext.Permissions
            .OrderBy(x => x.Module)
            .ThenBy(x => x.Action)
            .Select(x => new PermissionItemDto
            {
                Id = x.Id,
                PermCode = x.PermCode,
                Module = x.Module,
                Action = x.Action,
                MoTa = x.MoTa
            })
            .ToListAsync(cancellationToken);

        return Ok(ApiResponseFactory.Success<IReadOnlyCollection<PermissionItemDto>>(result));
    }
}
