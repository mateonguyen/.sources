using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ThucLuc.Api.Common.Authorization;
using ThucLuc.Api.Common.Models;
using ThucLuc.Application.Features.Users;
using ThucLuc.Application.Security;
using ThucLuc.Domain.Entities.Identity;

namespace ThucLuc.Api.Controllers.V1;

[ApiController]
[Route("api/v1/users")]
[ApiExplorerSettings(GroupName = "auth")]
public sealed class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    [HasPermission(Permissions.Users.Read)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<UserDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<UserDto>>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _userService.GetAllAsync(cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpPost]
    [HasPermission(Permissions.Users.Create)]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<UserDto>>> Create([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        var result = await _userService.CreateAsync(request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpPut("{id:long}")]
    [HasPermission(Permissions.Users.Update)]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<UserDto>>> Update(long id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var result = await _userService.UpdateAsync(id, request, cancellationToken);
        return Ok(ApiResponseFactory.Success(result));
    }

    [HttpPut("{id:long}/roles")]
    [HasPermission(Permissions.PhanQuyen.Update)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> AssignRoles(long id, [FromBody] AssignRolesRequest request, CancellationToken cancellationToken)
    {
        await _userService.AssignRolesAsync(id, request, cancellationToken);
        return Ok(ApiResponseFactory.Success<object>(null));
    }

    [HttpPost("transfers/preview")]
    [Authorize(Roles = "SYSTEM_ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<TransferUsersPreviewDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<TransferUsersPreviewDto>>> PreviewTransfers([FromBody] TransferUsersRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponseFactory.Success(await _userService.PreviewTransfersAsync(request, cancellationToken)));

    [HttpPost("transfers")]
    [Authorize(Roles = "SYSTEM_ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> TransferUsers([FromBody] TransferUsersRequest request, CancellationToken cancellationToken)
    {
        await _userService.TransferUsersAsync(request, cancellationToken);
        return Ok(ApiResponseFactory.Success<object>(null));
    }

    [HttpGet("{id:long}/transfers")]
    [Authorize(Roles = "SYSTEM_ADMIN")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<UserUnitHistory>>>> TransferHistory(long id, CancellationToken cancellationToken)
        => Ok(ApiResponseFactory.Success(await _userService.GetTransferHistoryAsync(id, cancellationToken)));
}
