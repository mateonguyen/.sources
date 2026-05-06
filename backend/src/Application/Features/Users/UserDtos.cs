namespace ThucLuc.Application.Features.Users;

public sealed class UserDto
{
    public long Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public string HoTen { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? SoDienThoai { get; set; }

    public long DonViId { get; set; }

    public bool IsActive { get; set; }

    public bool MustChangePassword { get; set; }
}

public sealed class CreateUserRequest
{
    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string HoTen { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? SoDienThoai { get; set; }

    public long DonViId { get; set; }
}

public sealed class UpdateUserRequest
{
    public string HoTen { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? SoDienThoai { get; set; }

    public bool IsActive { get; set; }

    public bool MustChangePassword { get; set; }
}

public sealed class AssignRolesRequest
{
    public IReadOnlyCollection<long> RoleIds { get; set; } = Array.Empty<long>();

    public long DonViId { get; set; }
}