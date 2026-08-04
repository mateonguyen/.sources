using System.Security.Cryptography;
using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using ThucLuc.Application.Common.Contracts;
using ThucLuc.Application.Common.Exceptions;
using ThucLuc.Domain.Entities.Identity;

namespace ThucLuc.Infrastructure.Identity;

/// <summary>
/// Implementation của IRefreshTokenService với token rotation và revocation support.
/// </summary>
public sealed class RefreshTokenService : IRefreshTokenService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    public RefreshTokenService(
        IApplicationDbContext dbContext,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<string> IssueRefreshTokenAsync(
        long userId,
        string deviceId,
        string deviceUserAgent,
        string deviceIpAddress,
        string? deviceName,
        int expiryDays,
        CancellationToken cancellationToken = default)
    {
        // Kiểm tra user tồn tại
        var userCount = await _dbContext.Users
            .Where(u => u.Id == userId)
            .CountAsync(cancellationToken);
        if (userCount == 0)
            throw new AppException("USER_NOT_FOUND", "Người dùng không tồn tại.", 404);

        // Tạo refresh token (random 64 bytes = 512 bits)
        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var tokenHash = BCrypt.Net.BCrypt.HashPassword(refreshToken);

        deviceId = NormalizeRequiredOracleText(deviceId, $"device-{Guid.NewGuid():N}");
        deviceUserAgent = NormalizeRequiredOracleText(deviceUserAgent, "unknown");
        deviceIpAddress = NormalizeRequiredOracleText(deviceIpAddress, "unknown");
        deviceName = NormalizeOptionalOracleText(deviceName);

        var now = _dateTimeProvider.Now;
        var session = new RefreshTokenSession
        {
            UserId = userId,
            RefreshTokenHash = tokenHash,
            DeviceId = deviceId,
            DeviceUserAgent = deviceUserAgent,
            DeviceIpAddress = deviceIpAddress,
            DeviceName = deviceName,
            IssuedAt = now,
            ExpiresAt = now.AddDays(expiryDays),
            IsRevoked = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.RefreshTokenSessions.Add(session);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return refreshToken;
    }

    public async Task<(string NewRefreshToken, RefreshTokenSession Session)> RotateRefreshTokenAsync(
        string refreshTokenFromCookie,
        long userId,
        string deviceId,
        string deviceUserAgent,
        string deviceIpAddress,
        CancellationToken cancellationToken = default)
    {
        // Tìm session theo deviceId và userId
        var session = await _dbContext.RefreshTokenSessions
            .FromSqlInterpolated($@"
                SELECT *
                FROM IDM_REFRESH_TOKEN_SESSIONS
                WHERE USER_ID = {userId}
                  AND DEVICE_ID = {deviceId}
                  AND IS_REVOKED = 0")
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new AppException("SESSION_NOT_FOUND", "Phiên không tồn tại hoặc đã bị thu hồi.", 401);

        // Verify token
        if (session.ExpiresAt < _dateTimeProvider.Now)
            throw new AppException("TOKEN_EXPIRED", "Refresh token đã hết hạn.", 401);

        if (!BCrypt.Net.BCrypt.Verify(refreshTokenFromCookie, session.RefreshTokenHash))
            throw new AppException("INVALID_TOKEN", "Refresh token không hợp lệ.", 401);

        // Revoke old token
        session.IsRevoked = true;
        session.RevokedAt = _dateTimeProvider.Now;
        session.RevocationReason = "TOKEN_ROTATED";

        // Issue new token
        var newRefreshToken = await IssueRefreshTokenAsync(
            userId,
            deviceId,
            deviceUserAgent,
            deviceIpAddress,
            session.DeviceName,
            (int)(session.ExpiresAt - _dateTimeProvider.Now).TotalDays,
            cancellationToken);

        // Update last used
        session.LastUsedAt = _dateTimeProvider.Now;
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Get new session để trả về
        var newSession = await _dbContext.RefreshTokenSessions
            .FromSqlInterpolated($@"
                SELECT *
                FROM IDM_REFRESH_TOKEN_SESSIONS
                WHERE USER_ID = {userId}
                  AND DEVICE_ID = {deviceId}
                  AND IS_REVOKED = 0
                ORDER BY ISSUED_AT DESC")
            .FirstAsync(cancellationToken);

        return (newRefreshToken, newSession);
    }

    public async Task RevokeRefreshTokenAsync(
        long userId,
        long sessionId,
        string revocationReason = "USER_LOGOUT",
        CancellationToken cancellationToken = default)
    {
        var session = await _dbContext.RefreshTokenSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId, cancellationToken)
            ?? throw new AppException("SESSION_NOT_FOUND", "Phiên không tồn tại.", 404);

        session.IsRevoked = true;
        session.RevokedAt = _dateTimeProvider.Now;
        session.RevocationReason = revocationReason;
        session.UpdatedAt = _dateTimeProvider.Now;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeAllUserRefreshTokensAsync(
        long userId,
        string revocationReason = "LOGOUT_ALL",
        CancellationToken cancellationToken = default)
    {
        var now = _dateTimeProvider.Now;
        var dbContext = (DbContext)_dbContext;
        await dbContext.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE IDM_REFRESH_TOKEN_SESSIONS
               SET IS_REVOKED = 1,
                   REVOKED_AT = {now},
                   REVOCATION_REASON = {revocationReason},
                   UPDATED_AT = {now}
             WHERE USER_ID = {userId}
               AND IS_REVOKED = 0", cancellationToken);
    }

    public async Task<IReadOnlyList<RefreshTokenSession>> GetActiveSessionsAsync(
        long userId,
        CancellationToken cancellationToken = default)
    {
        var now = _dateTimeProvider.Now;
        var sessions = await _dbContext.RefreshTokenSessions
            .FromSqlInterpolated($@"
                SELECT *
                FROM IDM_REFRESH_TOKEN_SESSIONS
                WHERE USER_ID = {userId}
                  AND IS_REVOKED = 0
                  AND EXPIRES_AT > {now}")
            .OrderByDescending(s => s.LastUsedAt ?? s.IssuedAt)
            .ToListAsync(cancellationToken);

        return sessions.AsReadOnly();
    }

    public async Task<int> CleanupExpiredTokensAsync(CancellationToken cancellationToken = default)
    {
        var now = _dateTimeProvider.Now;
        var expiredSessions = await _dbContext.RefreshTokenSessions
            .FromSqlInterpolated($@"
                SELECT *
                FROM IDM_REFRESH_TOKEN_SESSIONS
                WHERE EXPIRES_AT < {now}
                   OR (IS_REVOKED = 1 AND REVOKED_AT IS NOT NULL AND REVOKED_AT < {now.AddDays(-30)})")
            .ToListAsync(cancellationToken);

        if (expiredSessions.Count == 0)
            return 0;

        _dbContext.RefreshTokenSessions.RemoveRange(expiredSessions);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return expiredSessions.Count;
    }

    private static string NormalizeRequiredOracleText(string? value, string fallback)
    {
        var normalized = value?.Trim();
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            return normalized;
        }

        return fallback;
    }

    private static string? NormalizeOptionalOracleText(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
