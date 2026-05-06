using ThucLuc.Domain.Entities.System;

namespace ThucLuc.Domain.Entities.Identity;

/// <summary>
/// Lưu trữ refresh token sessions cho Level-3 "Remember Me".
/// Một user có thể có nhiều phiên trên các thiết bị khác nhau.
/// </summary>
public sealed class RefreshTokenSession
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public ApplicationUser? User { get; set; }

    /// <summary>
    /// Hash của refresh token (BCrypt) để so sánh an toàn.
    /// </summary>
    public string RefreshTokenHash { get; set; } = string.Empty;

    /// <summary>
    /// Device identifier (fingerprint hoặc UUID của thiết bị).
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// User-Agent của thiết bị khi cấp token.
    /// </summary>
    public string DeviceUserAgent { get; set; } = string.Empty;

    /// <summary>
    /// IP address khi cấp token.
    /// </summary>
    public string DeviceIpAddress { get; set; } = string.Empty;

    /// <summary>
    /// Tên thiết bị (ví dụ "Chrome on Windows", "Safari on iPhone").
    /// </summary>
    public string? DeviceName { get; set; }

    /// <summary>
    /// Thời gian cấp refresh token.
    /// </summary>
    public DateTime IssuedAt { get; set; }

    /// <summary>
    /// Thời gian hết hạn của refresh token.
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Lần cuối sử dụng refresh token (để track hoạt động).
    /// </summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    /// Có bị thu hồi hay không (logout, logout-all, hoặc bảo mật).
    /// </summary>
    public bool IsRevoked { get; set; } = false;

    /// <summary>
    /// Thời gian thu hồi (nếu có).
    /// </summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>
    /// Lý do thu hồi (ví dụ "USER_LOGOUT", "SECURITY_REVOKE", "PASSWORD_CHANGED").
    /// </summary>
    public string? RevocationReason { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
