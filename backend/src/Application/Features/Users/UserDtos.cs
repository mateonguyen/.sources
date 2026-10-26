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

    public IReadOnlyCollection<long> RoleIds { get; set; } = Array.Empty<long>();
}

public sealed class UpdateUserRequest
{
    public string HoTen { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? SoDienThoai { get; set; }

    public long DonViId { get; set; }

    public bool IsActive { get; set; }

    public bool MustChangePassword { get; set; }
}

public sealed class AssignRolesRequest
{
    public IReadOnlyCollection<long> RoleIds { get; set; } = Array.Empty<long>();

    public long DonViId { get; set; }
}

public sealed class TransferUsersRequest
{
    public IReadOnlyCollection<TransferUserItem> Items { get; set; } = Array.Empty<TransferUserItem>();
}

public sealed class TransferUserItem
{
    public long UserId { get; set; }
    public long SourceDonViId { get; set; }
    public long TargetDonViId { get; set; }
    public IReadOnlyCollection<long> RoleIds { get; set; } = Array.Empty<long>();
}

public sealed class TransferUsersPreviewDto
{
    public int UserCount { get; set; }
    public IReadOnlyCollection<string> Errors { get; set; } = Array.Empty<string>();
    public IReadOnlyCollection<TransferUserPreviewItem> Users { get; set; } = Array.Empty<TransferUserPreviewItem>();
}

public sealed class TransferUserPreviewItem
{
    public long UserId { get; set; }
    public string HoTen { get; set; } = string.Empty;
    public long SourceDonViId { get; set; }
    public long TargetDonViId { get; set; }
    public IReadOnlyCollection<long> OldRoleIds { get; set; } = Array.Empty<long>();
    public IReadOnlyCollection<long> NewRoleIds { get; set; } = Array.Empty<long>();
}
