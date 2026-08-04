using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ThucLuc.Api.Common.Authorization;
using ThucLuc.Api.Common.Models;
using ThucLuc.Application.Common.Contracts;
using ThucLuc.Application.Common.Exceptions;
using ThucLuc.Application.Features.Auth;
using ThucLuc.Application.Security;

namespace ThucLuc.Api.Controllers.V1;

[ApiController]
[Route("api/v1/auth")]
[ApiExplorerSettings(GroupName = "auth")]
public sealed class AuthController : ControllerBase
{
    private const string DeviceIdCookieName = "thuc_luc_device_id";

    private readonly IAuthService _authService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public AuthController(
        IAuthService authService,
        IRefreshTokenService refreshTokenService,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider)
    {
        _authService = authService;
        _refreshTokenService = refreshTokenService;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <summary>
    /// Endpoint login với hỗ trợ "Ghi nhớ đăng nhập" (Level-3).
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [ProducesResponseType(typeof(ApiResponse<AuthTokenDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<AuthTokenDto>>> Login(
        [FromBody] LoginRequest request,
        [FromQuery] bool rememberMe = false,
        CancellationToken cancellationToken = default)
    {
        var token = await _authService.LoginAsync(request, rememberMe, cancellationToken);

        var deviceId = ResolveDeviceId();
        var userAgent = ResolveUserAgent();
        var ipAddress = GetClientIpAddress();
        var refreshTokenExpiry = rememberMe ? 30 : 0; // 0 = session cookie

        // Cấp refresh token và lưu vào database. Nếu schema session chưa được migrate,
        // fallback về refresh token kiểu legacy để không chặn luồng đăng nhập.
        var refreshToken = token.RefreshToken;
        try
        {
            refreshToken = await _refreshTokenService.IssueRefreshTokenAsync(
                token.User!.Id,
                deviceId,
                userAgent,
                ipAddress,
                null,
                refreshTokenExpiry,
                cancellationToken);
        }
        catch
        {
            // Keep legacy refresh token issued by JwtTokenService.
        }

        // Set refresh token cookie
        DateTime? cookieExpiration = rememberMe
            ? _dateTimeProvider.Now.AddDays(30)
            : null; // Session cookie (hết khi đóng trình duyệt)

        Response.Cookies.Append("refresh_token", refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/api/v1/auth",
            Expires = cookieExpiration.HasValue ? new DateTimeOffset(cookieExpiration.Value) : null,
            IsEssential = true
        });

        Response.Cookies.Append(DeviceIdCookieName, deviceId, new CookieOptions
        {
            HttpOnly = false,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = new DateTimeOffset(_dateTimeProvider.Now.AddYears(1)),
            IsEssential = true
        });

        return Ok(ApiResponseFactory.Success(token));
    }

    /// <summary>
    /// Endpoint refresh token (token rotation + revocation của token cũ).
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("refresh")]
    [ProducesResponseType(typeof(ApiResponse<RefreshTokenResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<RefreshTokenResponse>>> Refresh(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        // Lấy refresh token từ cookie
        if (!Request.Cookies.TryGetValue("refresh_token", out var refreshTokenFromCookie))
            throw new AppException("REFRESH_TOKEN_MISSING", "Phiên đăng nhập đã hết hạn.", 401);

        // Lấy thông tin user từ access token nếu có hoặc từ request
        // Ở đây chúng ta giả định endpoint được call với Authorization header
        var currentUser = _currentUserService.GetCurrentUser();

        var deviceId = ResolveDeviceId(request.DeviceId);
        var userAgent = ResolveUserAgent(request.DeviceUserAgent);
        var ipAddress = GetClientIpAddress();

        // Rotate token
        var (newRefreshToken, session) = await _refreshTokenService.RotateRefreshTokenAsync(
            refreshTokenFromCookie,
            currentUser.UserId,
            deviceId,
            userAgent,
            ipAddress,
            cancellationToken);

        // Tạo access token mới (reuse existing logic hoặc call auth service)
        // Ở đây chúng ta return simplified response; trong production có thể gọi JwtTokenService
        var response = new RefreshTokenResponse
        {
            ExpiresIn = 900 // 15 minutes
        };

        // Set cookie refresh token mới
        Response.Cookies.Append("refresh_token", newRefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/api/v1/auth",
            Expires = new DateTimeOffset(session.ExpiresAt),
            IsEssential = true
        });

        Response.Cookies.Append(DeviceIdCookieName, deviceId, new CookieOptions
        {
            HttpOnly = false,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = new DateTimeOffset(_dateTimeProvider.Now.AddYears(1)),
            IsEssential = true
        });

        return Ok(ApiResponseFactory.Success(response));
    }

    /// <summary>
    /// Endpoint logout (revoke refresh token từ thiết bị hiện tại hoặc tất cả thiết bị).
    /// </summary>
    [HttpPost("logout")]
    [HasPermission(Permissions.Auth.Me)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> Logout(
        [FromBody] RevokeSessionRequest? request,
        CancellationToken cancellationToken = default)
    {
        var currentUser = _currentUserService.GetCurrentUser();

        if (request?.SessionId.HasValue == true)
        {
            // Logout từ một thiết bị cụ thể
            await _refreshTokenService.RevokeRefreshTokenAsync(
                currentUser.UserId,
                request.SessionId.Value,
                request.RevocationReason ?? "USER_LOGOUT",
                cancellationToken);
        }
        else
        {
            // Logout từ tất cả thiết bị
            await _refreshTokenService.RevokeAllUserRefreshTokensAsync(
                currentUser.UserId,
                "LOGOUT_ALL",
                cancellationToken);
        }

        // Xóa refresh token cookie
        Response.Cookies.Delete("refresh_token", new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/api/v1/auth"
        });

        return Ok(ApiResponseFactory.Success(new { message = "Logout thành công." }));
    }

    /// <summary>
    /// Endpoint lấy danh sách phiên đang hoạt động (quản lý phiên).
    /// </summary>
    [HttpGet("sessions")]
    [HasPermission(Permissions.Auth.Me)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SessionDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SessionDto>>>> GetSessions(
        CancellationToken cancellationToken = default)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        var sessions = await _refreshTokenService.GetActiveSessionsAsync(currentUser.UserId, cancellationToken);

        // Map sang SessionDto
        var sessionDtos = sessions.Select(s => new SessionDto
        {
            Id = s.Id,
            DeviceId = s.DeviceId,
            DeviceName = s.DeviceName,
            DeviceUserAgent = s.DeviceUserAgent,
            DeviceIpAddress = s.DeviceIpAddress,
            IssuedAt = s.IssuedAt,
            ExpiresAt = s.ExpiresAt,
            LastUsedAt = s.LastUsedAt,
            IsCurrentSession = false, // TODO: Xác định session hiện tại dựa vào device-id hoặc cookie
            IsRevoked = s.IsRevoked
        }).ToList().AsReadOnly();

        return Ok(ApiResponseFactory.Success(sessionDtos));
    }

    [HttpGet("me")]
    [HasPermission(Permissions.Auth.Me)]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<UserProfileDto>>> Me(CancellationToken cancellationToken)
    {
        var user = await _authService.GetCurrentUserAsync(cancellationToken);
        return Ok(ApiResponseFactory.Success(user));
    }

    /// <summary>
    /// Helper: Lấy IP address của client.
    /// </summary>
    private string GetClientIpAddress()
    {
        if (Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
            return forwardedFor.ToString().Split(',')[0].Trim();

        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    /// <summary>
    /// Helper: Tạo device ID nếu client không gửi (từ User-Agent hash).
    /// </summary>
    private string GenerateDeviceId()
    {
        return Guid.NewGuid().ToString("N");
    }

    private string ResolveDeviceId(string? requestDeviceId = null)
    {
        var fromHeader = Request.Headers["X-Device-Id"].ToString();
        var fromCookie = Request.Cookies.TryGetValue(DeviceIdCookieName, out var cookieValue)
            ? cookieValue
            : null;

        var candidates = new[] { requestDeviceId, fromHeader, fromCookie };
        foreach (var candidate in candidates)
        {
            var normalized = candidate?.Trim();
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                return normalized;
            }
        }

        return GenerateDeviceId();
    }

    private string ResolveUserAgent(string? requestUserAgent = null)
    {
        var normalizedRequest = requestUserAgent?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedRequest))
        {
            return normalizedRequest;
        }

        var headerUserAgent = Request.Headers["User-Agent"].ToString()?.Trim();
        if (!string.IsNullOrWhiteSpace(headerUserAgent))
        {
            return headerUserAgent;
        }

        return "unknown";
    }
}