using Microsoft.AspNetCore.Authorization;

namespace ThucLuc.Api.Common.Authorization;

public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public HasPermissionAttribute(string permission)
    {
        Policy = permission;
    }
}