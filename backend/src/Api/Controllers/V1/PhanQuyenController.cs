using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThucLuc.Api.Common.Authorization;
using ThucLuc.Api.Common.Models;
using ThucLuc.Application.Common.Contracts;
using ThucLuc.Application.Security;

namespace ThucLuc.Api.Controllers.V1;

public sealed class UserRoleMappingDto
{
    public long UserId { get; set; }

    public string Username { get; set; } = string.Empty;

    public long DonViId { get; set; }

    public IReadOnlyCollection<long> RoleIds { get; set; } = Array.Empty<long>();
}

public sealed class RolePermissionMappingDto
{
    public long RoleId { get; set; }

    public string RoleCode { get; set; } = string.Empty;

    public string TenRole { get; set; } = string.Empty;

    public IReadOnlyCollection<long> PermissionIds { get; set; } = Array.Empty<long>();
}

[ApiController]
[Route("api/v1/phan-quyen")]
[ApiExplorerSettings(GroupName = "auth")]
public sealed class PhanQuyenController : ControllerBase
{
    private readonly IApplicationDbContext _dbContext;

    public PhanQuyenController(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("user-roles")]
    [HasPermission(Permissions.Users.Read)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<UserRoleMappingDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<UserRoleMappingDto>>>> GetUserRoleMappings(
        [FromQuery] long? donViId,
        CancellationToken cancellationToken)
    {
        var users = _dbContext.Users.AsQueryable();
        if (donViId.HasValue)
        {
            users = users.Where(x => x.DonViId == donViId.Value);
        }

        var userList = await users
            .OrderBy(x => x.UserName)
            .Select(x => new { x.Id, Username = x.UserName ?? string.Empty, x.DonViId })
            .ToListAsync(cancellationToken);

        var userIds = userList.Select(x => x.Id).ToArray();
        var assignments = await _dbContext.UserRoleAssignments
            .Where(x => userIds.Contains(x.UserId))
            .ToListAsync(cancellationToken);

        var result = userList
            .Select(x => new UserRoleMappingDto
            {
                UserId = x.Id,
                Username = x.Username,
                DonViId = x.DonViId,
                RoleIds = assignments
                    .Where(a => a.UserId == x.Id && a.DonViId == x.DonViId)
                    .Select(a => a.RoleId)
                    .Distinct()
                    .ToArray()
            })
            .ToList();

        return Ok(ApiResponseFactory.Success<IReadOnlyCollection<UserRoleMappingDto>>(result));
    }

    [HttpGet("role-permissions")]
    [HasPermission(Permissions.Roles.Read)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<RolePermissionMappingDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<RolePermissionMappingDto>>>> GetRolePermissionMappings(
        CancellationToken cancellationToken)
    {
        var roles = await _dbContext.Roles
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, RoleCode = x.Name ?? string.Empty, x.TenRole })
            .ToListAsync(cancellationToken);

        var roleIds = roles.Select(x => x.Id).ToArray();
        var assignments = await _dbContext.RolePermissions
            .Where(x => roleIds.Contains(x.RoleId))
            .ToListAsync(cancellationToken);

        var result = roles
            .Select(x => new RolePermissionMappingDto
            {
                RoleId = x.Id,
                RoleCode = x.RoleCode,
                TenRole = x.TenRole,
                PermissionIds = assignments
                    .Where(a => a.RoleId == x.Id)
                    .Select(a => a.PermissionId)
                    .Distinct()
                    .ToArray()
            })
            .ToList();

        return Ok(ApiResponseFactory.Success<IReadOnlyCollection<RolePermissionMappingDto>>(result));
    }
}
