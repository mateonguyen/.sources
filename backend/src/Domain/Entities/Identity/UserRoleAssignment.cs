namespace ThucLuc.Domain.Entities.Identity;

public sealed class UserRoleAssignment
{
    public long UserId { get; set; }

    public long RoleId { get; set; }

    public long DonViId { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public ApplicationUser? User { get; set; }

    public ApplicationRole? Role { get; set; }
}