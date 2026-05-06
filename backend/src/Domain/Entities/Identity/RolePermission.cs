namespace ThucLuc.Domain.Entities.Identity;

public sealed class RolePermission
{
    public long RoleId { get; set; }

    public long PermissionId { get; set; }

    public ApplicationRole? Role { get; set; }

    public Permission? Permission { get; set; }
}