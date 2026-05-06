# Level-3 "Ghi nhớ Đăng Nhập" - Triển Khai Hoàn Chỉnh

## Tổng Quan

Triển khai Level-3 "Ghi nhớ đăng nhập" (Remember Me) cho hệ thống ThucLuc v2 với:

- **Token Rotation**: Access token ngắn hạn + Refresh token dài hạn
- **Server-Side Session Storage**: Lưu refresh token trên DB để quản lý vòng đời và revoke
- **Device Management**: Quản lý nhiều phiên trên các thiết bị khác nhau
- **Auto-Refresh**: Tự động làm mới access token khi hết hạn
- **MFA Hook**: Cảnh báo/yêu cầu MFA khi đăng nhập trên thiết bị mới
- **Audit Logging**: Ghi lại login/logout/refresh events

## Kiến Trúc

### Backend (.NET / C#)

#### Entities & Database

- `RefreshTokenSession` (bảng `IDM_REFRESH_TOKEN_SESSIONS`):
  - Lưu trữ refresh tokens cho mỗi thiết bị/session
  - Hỗ trợ token rotation, revocation, tracking (last used, device info)

#### Services

- `IRefreshTokenService` (contract) / `RefreshTokenService` (implementation):
  - `IssueRefreshTokenAsync()`: Cấp token mới, lưu DB
  - `RotateRefreshTokenAsync()`: Verify token, revoke cũ, issue mới (token rotation pattern)
  - `RevokeRefreshTokenAsync()`: Thu hồi một token (logout từ một thiết bị)
  - `RevokeAllUserRefreshTokensAsync()`: Thu hồi tất cả tokens (logout tất cả thiết bị)
  - `GetActiveSessionsAsync()`: Lấy danh sách phiên đang hoạt động
  - `CleanupExpiredTokensAsync()`: Cleanup tokens hết hạn (chạy định kỳ)

#### API Endpoints

- `POST /api/v1/auth/login?rememberMe=true/false`
  - Query param `rememberMe`: Quyết định thời gian sống của refresh token
  - Set cookie `refresh_token` (HttpOnly, Secure, SameSite=Lax)

- `POST /api/v1/auth/refresh`
  - Token rotation endpoint
  - Lấy refresh token từ cookie, rotate, set cookie mới

- `POST /api/v1/auth/logout`
  - Logout từ một thiết bị hoặc tất cả (tuỳ request body)
  - Xóa refresh token cookie

- `GET /api/v1/auth/sessions`
  - Lấy danh sách phiên đang hoạt động (cho Session Management UI)

### Frontend (Angular)

#### Models (auth.models.ts)

- `RefreshTokenRequest`, `RefreshTokenResponse`
- `Session` (session info để hiển thị)
- `RevokeSessionRequest`

#### Services (auth.service.ts)

- `login(username, password, rememberMe)`: Gọi backend login, tính toán expiry, lên lịch auto-refresh
- `refreshAccessToken()`: Token rotation logic (call /refresh, update token & expiry, reschedule)
- `getSessions()`: Lấy danh sách phiên
- `logout(logoutAll)`: Logout endpoint call + clear local state
- `scheduleTokenRefresh()`: Lên lịch auto-refresh trước 1 phút hết hạn

#### HTTP Interceptor (auth.interceptor.ts)

- Thêm Authorization header vào requests
- Catch 401 → retry với token mới (auto-refresh on 401)

#### Components

- `LoginPage` (login.page.ts, login.page.html, login.page.scss):
  - Checkbox "Ghi nhớ đăng nhập"
  - MFA warning khi đăng nhập trên thiết bị mới
  - Pass `rememberMe` flag vào `authService.login()`

- `SessionsManagementComponent` (sessions-management.component.ts):
  - Hiển thị danh sách phiên (thiết bị, browser, IP, last used, expiry)
  - Logout từ một thiết bị hoặc tất cả
  - (Integrate vào profile/settings page)

## Thời Gian Token

- **Access Token (AT)**: 15 phút
- **Refresh Token (RT)**:
  - Nếu `rememberMe=true`: 30 ngày
  - Nếu `rememberMe=false`: Session cookie (hết khi đóng trình duyệt)

## Cấu Hình

### appsettings.json

```json
{
  "Jwt": {
    "AccessTokenMinutes": 15,
    "RefreshTokenDays": 30,
    "SigningKey": "your-secret-key-min-32-chars",
    "Issuer": "ThucLuc",
    "Audience": "ThucLuc.API"
  }
}
```

### CORS & Cookie Policy

Trong `Program.cs`:

```csharp
// CORS cho refresh token cookie
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowCredentials", policy =>
    {
        policy.WithOrigins("http://localhost:4200", "https://yourdomain.com")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials(); // Important: cho phép cookie
    });
});

// Cookie policy
builder.Services.AddOptions<CookiePolicyOptions>().Configure(options =>
{
    options.Secure = CookieSecurePolicy.Always;
    options.HttpOnly = HttpOnlyPolicy.Always;
    options.SameSite = SameSiteMode.Lax;
});
```

### Angular HttpClient

```typescript
// main.ts
import { provideHttpClient, withXsrfConfiguration } from "@angular/common/http";

bootstrapApplication(AppComponent, {
  providers: [
    provideHttpClient(
      withInterceptors([authInterceptor]), // Auth interceptor
      withXsrfConfiguration({
        cookieName: "X-CSRF-TOKEN",
        headerName: "X-CSRF-TOKEN",
      }),
    ),
  ],
});
```

## Bảo Mật Best Practices

1. **Lưu Trữ Token Safely**:
   - ✅ Refresh token: HttpOnly, Secure cookie (không dùng localStorage)
   - ✅ Access token: Memory hoặc short-lived cookie
   - ❌ KHÔNG lưu refresh token ở localStorage (rủi ro XSS)

2. **Token Rotation**:
   - ✅ Mỗi lần dùng refresh token, cấp mới + revoke cũ
   - ✅ Server-side storage để track & revoke nhanh

3. **Rate Limiting**:
   - ✅ Limit endpoint `/login` và `/refresh` (vd. 5 request/phút per IP)
   - ✅ Detect brute-force attempts

4. **Device Fingerprinting** (MFA Enhancement):
   - ✅ Tính hash User-Agent + device info
   - ✅ Detect thiết bị mới → trigger MFA hoặc step-up auth

5. **Audit Logging**:
   - ✅ Log mỗi login, logout, refresh, revoke event
   - ✅ Lưu device info (IP, User-Agent, device-id) cho forensics

6. **Revocation & Cleanup**:
   - ✅ Logout immediately revoke token server-side
   - ✅ Periodic cleanup expired tokens (cron job)
   - ✅ Auto-revoke khi detect suspicious activity (nhiều refresh fail liên tục)

## Migration Steps

### 1. Database

```bash
# Tạo migration cho RefreshTokenSession
dotnet ef migrations add AddRefreshTokenSession -p src/Infrastructure -s src/Api

# Apply migration
dotnet ef database update -p src/Infrastructure -s src/Api
```

### 2. Backend

- ✅ Entities: `RefreshTokenSession` (created)
- ✅ Services: `IRefreshTokenService`, `RefreshTokenService` (created)
- ✅ Controllers: Updated `AuthController` với refresh, logout, sessions endpoints
- ✅ Configuration: `RefreshTokenSessionConfiguration` (created)
- ✅ DI: Registered `IRefreshTokenService` (done)

### 3. Frontend

- ✅ Models: Added refresh token DTOs (done)
- ✅ auth.service.ts: Added refresh logic, auto-schedule (done)
- ✅ auth.interceptor.ts: Added 401 retry with refresh (done)
- ✅ login.page: Added rememberMe checkbox + MFA hook (done)
- ✅ sessions-management.component: Created (done)

### 4. Integration

- Add `<app-sessions-management></app-sessions-management>` vào profile/settings page
- Test login flow với/không rememberMe
- Test auto-refresh & token rotation
- Test logout từ một thiết bị vs. tất cả

## Troubleshooting

### Token không được refresh

1. Kiểm tra cookie `refresh_token` có được set không (F12 → Application → Cookies)
2. Kiểm tra CORS policy cho phép `credentials: include`
3. Kiểm tra endpoint `/auth/refresh` có accessible không

### MFA Warning không hiển thị

1. `isNewDevice()` gọi `getSessions()` nhưng user chưa authenticated
2. Bọc `getSessions()` call trong try-catch

### Session không hiển thị

1. Check backend endpoint `/auth/sessions` trả về đúng session list
2. Check cookies được lưu sau khi login

### Auto-refresh không hoạt động

1. Check `scheduleTokenRefresh()` được gọi sau login
2. Check browser console có error không
3. Verify `tokenExpiryKey` được lưu chính xác ở localStorage

### Logout thất bại

1. Check backend logout endpoint xóa cookie có lỗi không
2. Check refresh token cookie xóa thành công (Set-Cookie expires in past)
3. Frontend `clearAuthState()` được call ngay cả khi API thất bại

## Tiếp Theo

1. **MFA Integration** (2FA/TOTP):
   - Add MFA challenge khi login trên thiết bị mới
   - Store MFA status ở RefreshTokenSession

2. **Device Approval Flow**:
   - Tạm dừng login trên thiết bị mới
   - Gửi email xác nhận
   - User approve → allow

3. **Geolocation Detection**:
   - Detect IP thay đổi drastically
   - Alert user về login bất thường

4. **Session Timeout**:
   - Inactive timeout (30 phút không hoạt động → logout)
   - Absolute timeout (12 giờ tối đa)

5. **Token Revocation List (TRL)**:
   - Maintain blacklist tokens bị revoke
   - Check trước khi accept request

## Tài Liệu Tham Khảo

- RFC 6749 (OAuth 2.0 Authorization Framework)
- RFC 6750 (OAuth 2.0 Bearer Token Usage)
- RFC 7234 (HTTP Caching)
- OWASP: Session Management Cheat Sheet
- NIST: Digital Identity Guidelines (SP 800-63)

---

**Status**: ✅ Triển khai hoàn tất
**Version**: 1.0
**Last Updated**: 2026-04-28
