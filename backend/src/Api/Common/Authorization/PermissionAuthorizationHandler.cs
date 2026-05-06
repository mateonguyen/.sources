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
        if (currentUser.HasPermission(Permissions.SystemAdmin) || currentUser.HasPermission(requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}