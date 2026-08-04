import { CommonModule } from '@angular/common';
import { Component, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputNumberModule } from 'primeng/inputnumber';
import { AuthService } from '../../core/auth/auth.service';
import { DonViApi } from '../don-vi/don-vi.api';
import { NotificationService } from '../../core/ui/notification.service';
import { LoadingOverlayComponent } from '../../shared/ui/loading-overlay.component';
import { SectionCardComponent } from '../../shared/ui/section-card.component';
import { TongHopModeBannerComponent } from '../../shared/ui/tong-hop-mode-banner.component';
import {
  HaTangMangApi,
  HaTangMangDto,
  SaveHaTangMangMatrixRequest,
  UpsertHaTangMangRequest,
} from './ha-tang-mang.api';

interface HaTangMangRow {
  id: number | null;
  soDonViTrucThuoc: number;
  soDaKetNoiBcanet: number;
  soDuongTruyenVnpt: number;
  soDuongTruyenKhac: number;
  soKetNoiInternet: number;
  ghiChu: string;
}

@Component({
  selector: 'app-ha-tang-mang-page',
  standalone: true,
  imports: [
    TongHopModeBannerComponent,
    CommonModule,
    FormsModule,
    SectionCardComponent,
    LoadingOverlayComponent,
    InputNumberModule,
    ButtonModule,
  ],
  templateUrl: './ha-tang-mang.page.html',
  styleUrl: './ha-tang-mang.page.scss',
})
export class HaTangMangPage {
  readonly donViId = computed(() => this.authService.profile()?.donViId ?? 0);
  readonly row = signal<HaTangMangRow | null>(null);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly directChildCount = signal(0);
  readonly hasValidationError = computed(() => {
    const current = this.row();
    if (!current) return false;

    return this.metricKeys.some(
      (key) => current[key] > current.soDonViTrucThuoc,
    );
  });

  private readonly metricKeys = [
    'soDaKetNoiBcanet',
    'soDuongTruyenVnpt',
    'soDuongTruyenKhac',
    'soKetNoiInternet',
  ] as const;

  constructor(
    private readonly authService: AuthService,
    private readonly haTangMangApi: HaTangMangApi,
    private readonly donViApi: DonViApi,
    private readonly notificationService: NotificationService,
  ) {
    void this.load();
  }

  async load(): Promise<void> {
    const donViId = this.donViId();
    if (!donViId) return;

    this.loading.set(true);
    try {
      const [items, donVi] = await Promise.all([
        this.haTangMangApi.getAll(donViId),
        this.donViApi.getById(donViId),
      ]);
      this.directChildCount.set(donVi.children.length);
      this.row.set(this.buildRow(items[0] ?? null, donVi.children.length));
    } finally {
      this.loading.set(false);
    }
  }

  async save(): Promise<void> {
    if (this.saving() || this.hasValidationError()) return;

    const donViId = this.donViId();
    if (!donViId) {
      this.notificationService.show(
        'error',
        'Không xác định được đơn vị hiện tại.',
      );
      return;
    }

    const r = this.row();
    if (!r) return;

    const payload: SaveHaTangMangMatrixRequest = {
      donViId,
      items: [this.toRequest(r, donViId)],
    };

    this.saving.set(true);
    try {
      const savedItems = await this.haTangMangApi.saveMatrix(payload);
      this.row.set(
        this.buildRow(savedItems[0] ?? null, this.directChildCount()),
      );
      this.notificationService.show(
        'success',
        'Lưu dữ liệu hạ tầng mạng thành công.',
      );
    } finally {
      this.saving.set(false);
    }
  }

  private buildRow(
    item: HaTangMangDto | null,
    defaultTrucThuocCount: number,
  ): HaTangMangRow {
    return {
      id: item?.id ?? null,
      soDonViTrucThuoc: item?.soDonViTrucThuoc ?? defaultTrucThuocCount,
      soDaKetNoiBcanet: item?.soDaKetNoiBcanet ?? 0,
      soDuongTruyenVnpt: item?.soDuongTruyenVnpt ?? 0,
      soDuongTruyenKhac: item?.soDuongTruyenKhac ?? 0,
      soKetNoiInternet: item?.soKetNoiInternet ?? 0,
      ghiChu: item?.ghiChu ?? '',
    };
  }

  private toRequest(
    r: HaTangMangRow,
    donViId: number,
  ): UpsertHaTangMangRequest {
    const normalize = (v: unknown): number => {
      const n = Number(v ?? 0);
      return Number.isFinite(n) && n >= 0 ? Math.trunc(n) : 0;
    };

    return {
      donViId,
      soDonViTrucThuoc: normalize(r.soDonViTrucThuoc),
      soDaKetNoiBcanet: normalize(r.soDaKetNoiBcanet),
      soDuongTruyenVnpt: normalize(r.soDuongTruyenVnpt),
      soDuongTruyenKhac: normalize(r.soDuongTruyenKhac),
      soKetNoiInternet: normalize(r.soKetNoiInternet),
      ghiChu: r.ghiChu.trim() || null,
    };
  }

  updateReadonlyChildCount(): void {
    const current = this.row();
    if (!current) return;

    this.row.set({
      ...current,
      soDonViTrucThuoc: this.directChildCount(),
    });
  }

  updateMetricValue(
    key:
      | 'soDaKetNoiBcanet'
      | 'soDuongTruyenVnpt'
      | 'soDuongTruyenKhac'
      | 'soKetNoiInternet',
    value: number | null | undefined,
  ): void {
    const current = this.row();
    if (!current) return;

    this.row.set({
      ...current,
      [key]: value ?? 0,
    });
  }

  isMetricExceeded(
    key:
      | 'soDaKetNoiBcanet'
      | 'soDuongTruyenVnpt'
      | 'soDuongTruyenKhac'
      | 'soKetNoiInternet',
  ): boolean {
    const current = this.row();
    return current ? current[key] > current.soDonViTrucThuoc : false;
  }
}
