namespace ThucLuc.Application.Common.Models;

public sealed class CurrentUserProfile
{
    public long UserId { get; init; }

    public string Username { get; init; } = string.Empty;

    public long DonViId { get; init; }

    public IReadOnlyCollection<string> Roles { get; init; } = Array.Empty<string>();

    public IReadOnlyCollection<string> Permissions { get; init; } = Array.Empty<string>();

    public bool IsAuthenticated => UserId > 0;

    public bool HasPermission(string permission) => Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);

    public bool IsInRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
}