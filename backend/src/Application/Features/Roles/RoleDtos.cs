namespace ThucLuc.Application.Features.Roles;

public sealed class RoleDto
{
    public long Id { get; set; }

    public string RoleCode { get; set; } = string.Empty;

    public string TenRole { get; set; } = string.Empty;

    public bool IsSystem { get; set; }

    public bool IsActive { get; set; } = true;

    public string? MoTa { get; set; }

    public IReadOnlyCollection<string> Permissions { get; set; } = Array.Empty<string>();
}

public sealed class CreateRoleRequest
{
    public string RoleCode { get; set; } = string.Empty;

    public string TenRole { get; set; } = string.Empty;

    public string? MoTa { get; set; }
}

public sealed class UpdateRolePermissionsRequest
{
    public IReadOnlyCollection<long> PermissionIds { get; set; } = Array.Empty<long>();
}

public sealed class UpdateRoleRequest
{
    public string RoleCode { get; set; } = string.Empty;

    public string TenRole { get; set; } = string.Empty;

    public string? MoTa { get; set; }
}