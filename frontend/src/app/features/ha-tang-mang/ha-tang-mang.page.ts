import { CommonModule } from '@angular/common';
import { Component, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { TableModule } from 'primeng/table';
import { TooltipModule } from 'primeng/tooltip';
import { AuthService } from '../../core/auth/auth.service';
import { CodesApi, CodeValueDto } from '../codes/codes.api';
import { DonViApi } from '../don-vi/don-vi.api';
import { NotificationService } from '../../core/ui/notification.service';
import { FilterBarComponent } from '../../shared/ui/filter-bar.component';
import { FormActionBarComponent } from '../../shared/ui/form-action-bar.component';
import { LoadingOverlayComponent } from '../../shared/ui/loading-overlay.component';
import { SectionCardComponent } from '../../shared/ui/section-card.component';
import {
  HaTangMangApi,
  HaTangMangDto,
  SaveHaTangMangMatrixRequest,
  UpsertHaTangMangRequest,
} from './ha-tang-mang.api';

interface HaTangMangRow {
  localId: string;
  id: number | null;
  loaiDvThongKe: string;
  loaiDvThongKeLabel: string;
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
    CommonModule,
    FormsModule,
    SectionCardComponent,
    FilterBarComponent,
    FormActionBarComponent,
    LoadingOverlayComponent,
    InputNumberModule,
    InputTextModule,
    ButtonModule,
    TableModule,
    TooltipModule,
  ],
  templateUrl: './ha-tang-mang.page.html',
  styleUrl: './ha-tang-mang.page.scss',
})
export class HaTangMangPage {
  readonly donViId = computed(() => this.authService.profile()?.donViId ?? 0);
  readonly loaiDvValues = signal<CodeValueDto[]>([]);
  readonly rows = signal<HaTangMangRow[]>([]);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly donViTrucThuocCount = signal(0);

  readonly totalSoDonViTrucThuoc = computed(() =>
    this.rows().reduce((sum, r) => sum + (r.soDonViTrucThuoc ?? 0), 0),
  );
  readonly totalSoDaKetNoiBcanet = computed(() =>
    this.rows().reduce((sum, r) => sum + (r.soDaKetNoiBcanet ?? 0), 0),
  );
  readonly totalSoDuongTruyenVnpt = computed(() =>
    this.rows().reduce((sum, r) => sum + (r.soDuongTruyenVnpt ?? 0), 0),
  );
  readonly totalSoDuongTruyenKhac = computed(() =>
    this.rows().reduce((sum, r) => sum + (r.soDuongTruyenKhac ?? 0), 0),
  );
  readonly totalSoKetNoiInternet = computed(() =>
    this.rows().reduce((sum, r) => sum + (r.soKetNoiInternet ?? 0), 0),
  );

  constructor(
    private readonly authService: AuthService,
    private readonly haTangMangApi: HaTangMangApi,
    private readonly codesApi: CodesApi,
    private readonly donViApi: DonViApi,
    private readonly notificationService: NotificationService,
  ) {
    void this.initialize();
  }

  async initialize(): Promise<void> {
    this.loading.set(true);
    try {
      const code = await this.codesApi.getByCode('LOAI_DV_THONG_KE_MANG');
      this.loaiDvValues.set(code.values.filter((v) => v.isActive));
      await this.load();
    } finally {
      this.loading.set(false);
    }
  }

  async load(): Promise<void> {
    const donViId = this.donViId();
    if (!donViId) {
      return;
    }

    this.loading.set(true);
    try {
      const [items, donVi] = await Promise.all([
        this.haTangMangApi.getAll(donViId),
        this.donViApi.getById(donViId),
      ]);
      const trucThuocCount = donVi.children.length;
      this.donViTrucThuocCount.set(trucThuocCount);
      this.rows.set(this.buildRows(items, trucThuocCount));
    } finally {
      this.loading.set(false);
    }
  }

  async save(): Promise<void> {
    if (this.saving()) {
      return;
    }

    const donViId = this.donViId();
    if (!donViId) {
      this.notificationService.show(
        'error',
        'Không xác định được đơn vị hiện tại.',
      );
      return;
    }

    const payload: SaveHaTangMangMatrixRequest = {
      donViId,
      items: this.rows().map((row) => this.toRequest(row, donViId)),
    };

    this.saving.set(true);
    try {
      const savedItems = await this.haTangMangApi.saveMatrix(payload);
      this.rows.set(this.buildRows(savedItems, this.donViTrucThuocCount()));
      this.notificationService.show(
        'success',
        'Lưu dữ liệu hạ tầng mạng thành công.',
      );
    } finally {
      this.saving.set(false);
    }
  }

  trackByRow(_: number, row: HaTangMangRow): string {
    return row.localId;
  }

  private buildRows(
    items: HaTangMangDto[],
    trucThuocCount: number,
  ): HaTangMangRow[] {
    return this.loaiDvValues().map((cv) => {
      const existing = items.find((it) => it.loaiDvThongKe === cv.value);
      return {
        localId: cv.value,
        id: existing?.id ?? null,
        loaiDvThongKe: cv.value,
        loaiDvThongKeLabel: cv.name,
        soDonViTrucThuoc: trucThuocCount,
        soDaKetNoiBcanet: existing?.soDaKetNoiBcanet ?? 0,
        soDuongTruyenVnpt: existing?.soDuongTruyenVnpt ?? 0,
        soDuongTruyenKhac: existing?.soDuongTruyenKhac ?? 0,
        soKetNoiInternet: existing?.soKetNoiInternet ?? 0,
        ghiChu: existing?.ghiChu ?? '',
      };
    });
  }

  private toRequest(
    row: HaTangMangRow,
    donViId: number,
  ): UpsertHaTangMangRequest {
    return {
      donViId,
      loaiDvThongKe: row.loaiDvThongKe,
      soDonViTrucThuoc: row.soDonViTrucThuoc,
      soDaKetNoiBcanet: row.soDaKetNoiBcanet,
      soDuongTruyenVnpt: row.soDuongTruyenVnpt,
      soDuongTruyenKhac: row.soDuongTruyenKhac,
      soKetNoiInternet: row.soKetNoiInternet,
      ghiChu: row.ghiChu.trim() || null,
    };
  }
}
