using Microsoft.AspNetCore.Authorization;
using ThucLuc.Application.Common.Contracts;
using ThucLuc.Application.Security;

namespace ThucLuc.Api.Common.Authorization;

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly ICurrentUserService _currentUserService;

    public PermissionAuthorizationHandler(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        var hasSystemAdmin = currentUser.HasPermission(Permissions.SystemAdmin);
        var isBusinessPermission = Permissions.IsBusinessPermission(requirement.Permission);
        var isSystemAdminExcluded = Permissions.IsSystemAdminExcluded(requirement.Permission);
        var isRestrictedBusinessRole =
            currentUser.IsInRole("SYSTEM_ADMIN") || currentUser.IsInRole("QUAN_LY");

        if (isBusinessPermission && isRestrictedBusinessRole)
        {
            return Task.CompletedTask;
        }

        if ((hasSystemAdmin && !isBusinessPermission && !isSystemAdminExcluded) || currentUser.HasPermission(requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}