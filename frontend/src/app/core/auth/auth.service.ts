import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '../api/api.constants';
import {
  ApiResponse,
  AuthToken,
  UserProfile,
  RefreshTokenRequest,
  RefreshTokenResponse,
  Session,
  RevokeSessionRequest,
} from './auth.models';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly tokenKey = 'thuc_luc_access_token';
  private readonly userKey = 'thuc_luc_user_profile';
  private readonly deviceIdKey = 'thuc_luc_device_id';
  private readonly tokenExpiryKey = 'thuc_luc_token_expiry';
  private refreshTokenTimeout?: any;

  readonly token = signal<string | null>(localStorage.getItem(this.tokenKey));
  readonly profile = signal<UserProfile | null>(this.loadProfile());
  readonly isAuthenticated = signal(!!localStorage.getItem(this.tokenKey));

  constructor(
    private readonly httpClient: HttpClient,
    private readonly router: Router,
  ) {
    // Khởi tạo auto-refresh nếu user đã đăng nhập
    if (this.isAuthenticated()) {
      this.scheduleTokenRefresh();
    }
  }

  async login(
    username: string,
    password: string,
    rememberMe: boolean = false,
  ): Promise<void> {
    const response = await firstValueFrom(
      this.httpClient.post<ApiResponse<AuthToken>>(
        `${API_BASE_URL}/auth/login?rememberMe=${rememberMe}`,
        { username, password },
      ),
    );

    const deviceId = this.getOrCreateDeviceId();

    localStorage.setItem(this.tokenKey, response.data.accessToken);
    localStorage.setItem(this.userKey, JSON.stringify(response.data.user));
    localStorage.setItem(
      this.tokenExpiryKey,
      new Date(response.data.expiresAt).getTime().toString(),
    );

    this.token.set(response.data.accessToken);
    this.profile.set(response.data.user);
    this.isAuthenticated.set(true);

    // Bắt đầu auto-refresh scheduler
    this.scheduleTokenRefresh();
  }

  async refreshProfile(): Promise<void> {
    const response = await firstValueFrom(
      this.httpClient.get<ApiResponse<UserProfile>>(`${API_BASE_URL}/auth/me`),
    );
    localStorage.setItem(this.userKey, JSON.stringify(response.data));
    this.profile.set(response.data);
  }

  /**
   * Refresh access token bằng refresh token từ cookie.
   * Token rotation được xử lý bởi backend (revoke old token + issue new).
   */
  async refreshAccessToken(): Promise<string> {
    try {
      const deviceId = this.getOrCreateDeviceId();
      const request: RefreshTokenRequest = {
        deviceId,
        deviceUserAgent: navigator.userAgent,
      };

      const response = await firstValueFrom(
        this.httpClient.post<ApiResponse<RefreshTokenResponse>>(
          `${API_BASE_URL}/auth/refresh`,
          request,
        ),
      );

      // Cập nhật access token và expiry
      localStorage.setItem(this.tokenKey, response.data.accessToken);
      localStorage.setItem(
        this.tokenExpiryKey,
        (Date.now() + response.data.expiresIn * 1000).toString(),
      );

      this.token.set(response.data.accessToken);

      // Nếu response chứa user info (thay đổi permissions, etc.), cập nhật
      if (response.data.user) {
        localStorage.setItem(this.userKey, JSON.stringify(response.data.user));
        this.profile.set(response.data.user);
      }

      // Lên lịch refresh mới
      this.scheduleTokenRefresh();

      return response.data.accessToken;
    } catch (error) {
      // Nếu refresh thất bại, logout
      console.error('Token refresh failed:', error);
      this.logout();
      throw error;
    }
  }

  /**
   * Lấy danh sách phiên đang hoạt động (cho Session Management component).
   */
  async getSessions(): Promise<Session[]> {
    const response = await firstValueFrom(
      this.httpClient.get<ApiResponse<Session[]>>(
        `${API_BASE_URL}/auth/sessions`,
      ),
    );
    return response.data;
  }

  /**
   * Logout từ thiết bị hiện tại hoặc tất cả thiết bị.
   */
  async logout(logoutAll: boolean = false): Promise<void> {
    try {
      const request: RevokeSessionRequest = {
        sessionId: logoutAll ? undefined : undefined, // undefined = logout all nếu logoutAll = true
        revocationReason: logoutAll ? 'LOGOUT_ALL' : 'USER_LOGOUT',
      };

      await firstValueFrom(
        this.httpClient.post<ApiResponse<{ message: string }>>(
          `${API_BASE_URL}/auth/logout`,
          request,
        ),
      );
    } catch (error) {
      console.error('Logout API call failed:', error);
      // Vẫn clear local state ngay cả khi API call thất bại
    }

    // Clear local state
    this.clearAuthState();
  }

  hasPermission(permission: string): boolean {
    const profile = this.profile();
    if (!profile) {
      return false;
    }

    return (
      profile.permissions.includes('system:admin') ||
      profile.permissions.includes(permission)
    );
  }

  /**
   * Lấy hoặc tạo device ID duy nhất cho thiết bị này.
   */
  private getOrCreateDeviceId(): string {
    let deviceId = localStorage.getItem(this.deviceIdKey);
    if (!deviceId) {
      // Tạo device ID mới (UUID hoặc hash của User-Agent)
      deviceId = this.generateDeviceId();
      localStorage.setItem(this.deviceIdKey, deviceId);
    }
    return deviceId;
  }

  /**
   * Tạo device ID duy nhất (SHA256 hash của User-Agent + timestamp).
   */
  private generateDeviceId(): string {
    const data = navigator.userAgent + Date.now();
    // Nếu Web Crypto API khả dụng, dùng SHA256; nếu không, dùng UUID hoặc simple hash
    return btoa(data).substring(0, 32); // Simple base64 hash (32 chars)
  }

  /**
   * Lên lịch auto-refresh của access token (trước 1 phút khi hết hạn).
   */
  private scheduleTokenRefresh(): void {
    // Hủy refresh timeout cũ nếu có
    if (this.refreshTokenTimeout) {
      clearTimeout(this.refreshTokenTimeout);
    }

    const expiryTime = parseInt(
      localStorage.getItem(this.tokenExpiryKey) || '0',
      10,
    );
    if (!expiryTime) {
      return;
    }

    const now = Date.now();
    const timeUntilExpiry = expiryTime - now;
    const refreshBefore = 60 * 1000; // Refresh 1 phút trước hết hạn

    if (timeUntilExpiry <= 0) {
      // Token đã hết hạn, refresh ngay
      this.refreshAccessToken().catch(() => {
        /* logout đã xảy ra trong refreshAccessToken */
      });
      return;
    }

    const delayUntilRefresh = Math.max(0, timeUntilExpiry - refreshBefore);

    this.refreshTokenTimeout = setTimeout(() => {
      this.refreshAccessToken().catch(() => {
        /* logout đã xảy ra trong refreshAccessToken */
      });
    }, delayUntilRefresh);
  }

  /**
   * Clear authentication state (logout).
   */
  private clearAuthState(): void {
    localStorage.removeItem(this.tokenKey);
    localStorage.removeItem(this.userKey);
    localStorage.removeItem(this.tokenExpiryKey);
    this.token.set(null);
    this.profile.set(null);
    this.isAuthenticated.set(false);

    if (this.refreshTokenTimeout) {
      clearTimeout(this.refreshTokenTimeout);
    }

    this.router.navigate(['/login']);
  }

  private loadProfile(): UserProfile | null {
    const raw = localStorage.getItem(this.userKey);
    if (!raw) {
      return null;
    }

    try {
      return JSON.parse(raw) as UserProfile;
    } catch {
      return null;
    }
  }
}
