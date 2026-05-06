namespace ThucLuc.Application.Features.Auth;

public sealed class LoginRequest
{
    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}

public sealed class AuthTokenDto
{
    public string AccessToken { get; set; } = string.Empty;

    public int ExpiresIn { get; set; }

    public string RefreshToken { get; set; } = string.Empty;

    public UserProfileDto User { get; set; } = new();
}

public sealed class UserProfileDto
{
    public long Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public string HoTen { get; set; } = string.Empty;

    public long DonViId { get; set; }

    public IReadOnlyCollection<string> Roles { get; set; } = Array.Empty<string>();

    public IReadOnlyCollection<string> Permissions { get; set; } = Array.Empty<string>();

    public bool MustChangePassword { get; set; }
}