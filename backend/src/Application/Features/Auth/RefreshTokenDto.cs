namespace ThucLuc.Application.Features.Auth;

/// <summary>
/// DTO để cấp refresh token (sau khi verify access token hoặc từ cookie).
/// </summary>
public sealed class RefreshTokenRequest
{
    /// <summary>
    /// Mã thiết bị (từ frontend) để phân biệt các phiên.
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// User-Agent của trình duyệt/ứng dụng.
    /// </summary>
    public string DeviceUserAgent { get; set; } = string.Empty;

    /// <summary>
    /// Tên thiết bị (ví dụ "Chrome on Windows").
    /// </summary>
    public string? DeviceName { get; set; }
}

/// <summary>
/// Response của refresh token endpoint.
/// </summary>
public sealed class RefreshTokenResponse
{
    public string AccessToken { get; set; } = string.Empty;

    public int ExpiresIn { get; set; }

    public UserProfileDto? User { get; set; }
}

/// <summary>
/// DTO để hiển thị thông tin phiên đang hoạt động.
/// </summary>
public sealed class SessionDto
{
    public long Id { get; set; }

    public string DeviceId { get; set; } = string.Empty;

    public string? DeviceName { get; set; }

    public string DeviceUserAgent { get; set; } = string.Empty;

    public string DeviceIpAddress { get; set; } = string.Empty;

    public DateTime IssuedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? LastUsedAt { get; set; }

    public bool IsCurrentSession { get; set; }

    public bool IsRevoked { get; set; }
}

/// <summary>
/// Request để logout từ một thiết bị cụ thể.
/// </summary>
public sealed class RevokeSessionRequest
{
    /// <summary>
    /// Session ID để revoke. Nếu null, revoke tất cả.
    /// </summary>
    public long? SessionId { get; set; }

    /// <summary>
    /// Lý do revoke (ví dụ "USER_LOGOUT", "LOGOUT_ALL").
    /// </summary>
    public string RevocationReason { get; set; } = "USER_LOGOUT";
}
