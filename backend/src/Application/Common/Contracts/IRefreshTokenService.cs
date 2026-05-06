using ThucLuc.Domain.Entities.Identity;

namespace ThucLuc.Application.Common.Contracts;

/// <summary>
/// Service quản lý refresh token sessions cho Level-3 "Remember Me".
/// Hỗ trợ token rotation, revocation, và session management.
/// </summary>
public interface IRefreshTokenService
{
    /// <summary>
    /// Cấp refresh token mới cho user, lưu vào DB, và trả về token plaintext.
    /// </summary>
    Task<string> IssueRefreshTokenAsync(
        long userId,
        string deviceId,
        string deviceUserAgent,
        string deviceIpAddress,
        string? deviceName,
        int expiryDays,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verify refresh token từ cookie, rotate (cấp mới + revoke cũ), và trả token mới.
    /// </summary>
    Task<(string NewRefreshToken, RefreshTokenSession Session)> RotateRefreshTokenAsync(
        string refreshTokenFromCookie,
        long userId,
        string deviceId,
        string deviceUserAgent,
        string deviceIpAddress,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Thu hồi refresh token (logout từ một thiết bị).
    /// </summary>
    Task RevokeRefreshTokenAsync(
        long userId,
        long sessionId,
        string revocationReason = "USER_LOGOUT",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Thu hồi tất cả refresh tokens của user (logout ở tất cả thiết bị).
    /// </summary>
    Task RevokeAllUserRefreshTokensAsync(
        long userId,
        string revocationReason = "LOGOUT_ALL",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy danh sách phiên đang hoạt động của user.
    /// </summary>
    Task<IReadOnlyList<RefreshTokenSession>> GetActiveSessionsAsync(
        long userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Xóa các refresh token hết hạn (cleanup định kỳ).
    /// </summary>
    Task<int> CleanupExpiredTokensAsync(CancellationToken cancellationToken = default);
}
