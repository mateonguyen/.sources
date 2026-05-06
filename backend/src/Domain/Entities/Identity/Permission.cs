using ThucLuc.Domain.Common.Base;

namespace ThucLuc.Domain.Entities.Identity;

public sealed class Permission : EntityBase
{
    public string PermCode { get; set; } = string.Empty;

    public string Module { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string? MoTa { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}