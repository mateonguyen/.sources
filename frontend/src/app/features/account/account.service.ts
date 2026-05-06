import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '../../core/api/api.constants';
import { ApiResponse } from '../../core/auth/auth.models';
import { AuthService } from '../../core/auth/auth.service';

export interface UpdateProfileRequest {
  hoTen: string;
  email: string | null;
  soDienThoai: string | null;
}

export interface ChangePasswordRequest {
  matKhauHienTai: string;
  matKhauMoi: string;
}

export interface AccountDetail {
  userId: number;
  username: string;
  hoTen: string;
  email: string | null;
  soDienThoai: string | null;
  donViId: number;
  donViTen: string | null;
  roles: string[];
}

@Injectable({ providedIn: 'root' })
export class AccountService {
  constructor(
    private readonly http: HttpClient,
    private readonly authService: AuthService,
  ) {}

  async getProfile(): Promise<AccountDetail> {
    try {
      const res = await firstValueFrom(
        this.http.get<ApiResponse<AccountDetail>>(`${API_BASE_URL}/auth/me`),
      );
      return res.data;
    } catch {
      // Fallback: build from cached token profile
      const p = this.authService.profile();
      if (!p) {
        throw new Error('Không thể tải thông tin tài khoản.');
      }
      return {
        userId: p.userId,
        username: p.username,
        hoTen: p.hoTen,
        email: null,
        soDienThoai: null,
        donViId: p.donViId,
        donViTen: null,
        roles: p.roles,
      };
    }
  }

  async updateProfile(request: UpdateProfileRequest): Promise<void> {
    await firstValueFrom(
      this.http.put<ApiResponse<unknown>>(
        `${API_BASE_URL}/auth/profile`,
        request,
      ),
    );
    // Refresh cached profile
    await this.authService.refreshProfile();
  }

  async changePassword(request: ChangePasswordRequest): Promise<void> {
    await firstValueFrom(
      this.http.post<ApiResponse<unknown>>(
        `${API_BASE_URL}/auth/change-password`,
        request,
      ),
    );
  }
}
