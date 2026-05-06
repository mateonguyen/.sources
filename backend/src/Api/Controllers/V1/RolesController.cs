using Microsoft.AspNetCore.Mvc;
using ThucLuc.Api.Common.Authorization;
using ThucLuc.Api.Common.Models;
using ThucLuc.Application.Features.Roles;
using ThucLuc.Application.Security;

namespace ThucLuc.Api.Controllers.V1;

[ApiController]
[Route("api/v1/roles")]
[ApiExplorerSettings(GroupName = "auth")]
public sealed class RolesController : ControllerBase
{
    private readonly IRoleService _roleService;

    public RolesController(IRoleService roleService)
    {
        _roleService = roleService;
    }

    [HttpGet]
    [HasPermission(Permissions.Roles.Read)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<RoleDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<RoleDto>>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _roleService.GetAllAsync(cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpPost]
    [HasPermission(Permissions.Roles.Create)]
    [ProducesResponseType(typeof(ApiResponse<RoleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<RoleDto>>> Create([FromBody] CreateRoleRequest request, CancellationToken cancellationToken)
    {
        var result = await _roleService.CreateAsync(request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpPut("{id:long}/permissions")]
    [HasPermission(Permissions.Roles.Update)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> UpdatePermissions(long id, [FromBody] UpdateRolePermissionsRequest request, CancellationToken cancellationToken)
    {
        await _roleService.UpdatePermissionsAsync(id, request, cancellationToken);
        return Ok(ApiResponseFactory.Success<object>(null));
    }

    [HttpPut("{id:long}")]
    [HasPermission(Permissions.Roles.Update)]
    [ProducesResponseType(typeof(ApiResponse<RoleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<RoleDto>>> Update(long id, [FromBody] UpdateRoleRequest request, CancellationToken cancellationToken)
    {
        var result = await _roleService.UpdateAsync(id, request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpDelete("{id:long}")]
    [HasPermission(Permissions.Roles.Update)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        await _roleService.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponseFactory.Success<object>(null));
    }
}
