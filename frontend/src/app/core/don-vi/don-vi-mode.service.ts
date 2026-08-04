import { Injectable, signal } from '@angular/core';
import { AuthService } from '../auth/auth.service';
import { DonViApi } from '../../features/don-vi/don-vi.api';

/**
 * Chế độ nhập liệu của đơn vị user hiện tại (TU_NHAP | TONG_HOP).
 * Dùng để ẩn menu/trang nghiệp vụ khi đơn vị ở chế độ Tổng hợp.
 */
@Injectable({ providedIn: 'root' })
export class DonViModeService {
  /** null = chưa tải xong; mặc định coi như TU_NHAP để không ẩn nhầm menu. */
  readonly cheDoNhapLieu = signal<'TU_NHAP' | 'TONG_HOP' | null>(null);

  private loadPromise: Promise<void> | null = null;
  private loadedForDonViId: number | null = null;

  constructor(
    private readonly authService: AuthService,
    private readonly donViApi: DonViApi,
  ) {}

  get isTongHop(): boolean {
    return this.cheDoNhapLieu() === 'TONG_HOP';
  }

  ensureLoaded(): Promise<void> {
    const donViId = this.authService.profile()?.donViId ?? null;
    if (!donViId) {
      this.cheDoNhapLieu.set(null);
      this.loadedForDonViId = null;
      return Promise.resolve();
    }

    if (this.loadPromise && this.loadedForDonViId === donViId) {
      return this.loadPromise;
    }

    this.loadedForDonViId = donViId;
    this.loadPromise = this.donViApi
      .getById(donViId)
      .then((donVi) => {
        const mode = (donVi.cheDoNhapLieu ?? '').trim().toUpperCase();
        this.cheDoNhapLieu.set(mode === 'TONG_HOP' ? 'TONG_HOP' : 'TU_NHAP');
      })
      .catch(() => {
        // Không xác định được thì coi như TU_NHAP (không ẩn menu nhầm).
        this.cheDoNhapLieu.set('TU_NHAP');
      });

    return this.loadPromise;
  }
}
