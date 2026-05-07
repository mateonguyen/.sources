import { CommonModule } from '@angular/common';
import { Component, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DropdownModule } from 'primeng/dropdown';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputSwitchModule } from 'primeng/inputswitch';
import { InputTextModule } from 'primeng/inputtext';
import { TooltipModule } from 'primeng/tooltip';
import { AuthService } from '../../core/auth/auth.service';
import { CodesApi, CodeValueDto } from '../codes/codes.api';
import { NotificationService } from '../../core/ui/notification.service';
import { FilterBarComponent } from '../../shared/ui/filter-bar.component';
import { LoadingOverlayComponent } from '../../shared/ui/loading-overlay.component';
import { SectionCardComponent } from '../../shared/ui/section-card.component';
import {
  GiamSatSocApi,
  GiamSatSocDto,
  SaveGiamSatSocMatrixRequest,
  UpsertGiamSatSocRequest,
} from './giam-sat-soc.api';

interface SelectOption {
  label: string;
  value: string;
}

interface GiamSatSocRow {
  localId: string;
  id: number | null;
  loaiMang: string;
  lopGiamSat: string;
  lopGiamSatLabel: string;
  coHeThong: boolean;
  thucTrang: string | null;
  tongSoDoiTuong: number;
  soGiamSatMotPhan: number;
  soGiamSatCoBan: number;
  soGiamSatDayDu: number;
  soSuCo: number;
  soSuCoDaKhacPhuc: number;
  lucLuongUngCuu: string;
  ghiChu: string;
}

interface LoaiMangGroup {
  value: string;
  label: string;
  rows: GiamSatSocRow[];
}

@Component({
  selector: 'app-giam-sat-soc-page',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    SectionCardComponent,
    FilterBarComponent,
    LoadingOverlayComponent,
    DropdownModule,
    InputNumberModule,
    InputSwitchModule,
    InputTextModule,
    ButtonModule,
    TooltipModule,
  ],
  templateUrl: './giam-sat-soc.page.html',
  styleUrl: './giam-sat-soc.page.scss',
})
export class GiamSatSocPage {
  readonly donViId = computed(() => this.authService.profile()?.donViId ?? 0);

  readonly loaiMangValues = signal<CodeValueDto[]>([]);
  readonly lopGiamSatValues = signal<CodeValueDto[]>([]);
  readonly thucTrangValues = signal<CodeValueDto[]>([]);
  readonly rows = signal<GiamSatSocRow[]>([]);
  readonly loading = signal(false);
  readonly saving = signal(false);

  readonly thucTrangOptions = computed<SelectOption[]>(() =>
    this.thucTrangValues().map((item) => ({
      label: item.name,
      value: item.value,
    })),
  );

  readonly loaiMangGroups = computed<LoaiMangGroup[]>(() => {
    const allRows = this.rows();
    return this.loaiMangValues().map((lm) => ({
      value: lm.value,
      label: lm.name,
      rows: allRows.filter((r) => r.loaiMang === lm.value),
    }));
  });

  readonly hasValidationError = computed(() =>
    this.rows().some(
      (r) =>
        r.soGiamSatMotPhan + r.soGiamSatCoBan + r.soGiamSatDayDu >
          r.tongSoDoiTuong || r.soSuCoDaKhacPhuc > r.soSuCo,
    ),
  );

  constructor(
    private readonly authService: AuthService,
    private readonly giamSatSocApi: GiamSatSocApi,
    private readonly codesApi: CodesApi,
    private readonly notificationService: NotificationService,
  ) {
    void this.initialize();
  }

  async initialize(): Promise<void> {
    this.loading.set(true);
    try {
      const [loaiMangCode, lopGiamSatCode, thucTrangCode] = await Promise.all([
        this.codesApi.getByCode('LOAI_MANG_GIAM_SAT'),
        this.codesApi.getByCode('LOP_GIAM_SAT'),
        this.codesApi.getByCode('THUC_TRANG_GIAM_SAT'),
      ]);
      this.loaiMangValues.set(loaiMangCode.values);
      this.lopGiamSatValues.set(lopGiamSatCode.values);
      this.thucTrangValues.set(thucTrangCode.values);
      await this.load();
    } finally {
      this.loading.set(false);
    }
  }

  async load(): Promise<void> {
    const donViId = this.donViId();
    if (!donViId) return;
    this.loading.set(true);
    try {
      const items = await this.giamSatSocApi.getAll({ donViId });
      this.rows.set(this.buildRows(items));
    } finally {
      this.loading.set(false);
    }
  }

  async save(): Promise<void> {
    if (this.saving()) return;
    const donViId = this.donViId();
    if (!donViId) {
      this.notificationService.show('error', 'Không xác định được đơn vị hiện tại.');
      return;
    }
    const payload: SaveGiamSatSocMatrixRequest = {
      donViId,
      items: this.rows().map((row) => this.toRequest(row, donViId)),
    };
    this.saving.set(true);
    try {
      const savedItems = await this.giamSatSocApi.saveMatrix(payload);
      this.rows.set(this.buildRows(savedItems));
      this.notificationService.show('success', 'Lưu ma trận giám sát SOC thành công.');
    } finally {
      this.saving.set(false);
    }
  }

  getCoHeThong(loaiMang: string): boolean {
    return this.rows().find((r) => r.loaiMang === loaiMang)?.coHeThong ?? false;
  }

  onCoHeThongChange(loaiMang: string, checked: boolean): void {
    this.rows.update((items) =>
      items.map((item) =>
        item.loaiMang === loaiMang ? { ...item, coHeThong: checked } : item,
      ),
    );
  }

  hasGiamSatError(row: GiamSatSocRow): boolean {
    return (
      row.soGiamSatMotPhan + row.soGiamSatCoBan + row.soGiamSatDayDu >
      row.tongSoDoiTuong
    );
  }

  hasSuCoError(row: GiamSatSocRow): boolean {
    return row.soSuCoDaKhacPhuc > row.soSuCo;
  }

  hasRowError(row: GiamSatSocRow): boolean {
    return this.hasGiamSatError(row) || this.hasSuCoError(row);
  }

  trackByGroup(_: number, group: LoaiMangGroup): string {
    return group.value;
  }

  trackByRow(_: number, row: GiamSatSocRow): string {
    return row.localId;
  }

  private buildRows(items: GiamSatSocDto[]): GiamSatSocRow[] {
    const result: GiamSatSocRow[] = [];
    for (const loaiMang of this.loaiMangValues()) {
      const groupItems = items.filter((item) => item.loaiMang === loaiMang.value);
      const coHeThong = groupItems[0]?.coHeThong ?? false;
      for (const lopGiamSat of this.lopGiamSatValues()) {
        const matched = groupItems.find((item) => item.lopGiamSat === lopGiamSat.value);
        result.push({
          localId: `${loaiMang.value}-${lopGiamSat.value}`,
          id: matched?.id ?? null,
          loaiMang: loaiMang.value,
          lopGiamSat: lopGiamSat.value,
          lopGiamSatLabel: lopGiamSat.name,
          coHeThong,
          thucTrang: matched?.thucTrang ?? null,
          tongSoDoiTuong: matched?.tongSoDoiTuong ?? 0,
          soGiamSatMotPhan: matched?.soGiamSatMotPhan ?? 0,
          soGiamSatCoBan: matched?.soGiamSatCoBan ?? 0,
          soGiamSatDayDu: matched?.soGiamSatDayDu ?? 0,
          soSuCo: matched?.soSuCo ?? 0,
          soSuCoDaKhacPhuc: matched?.soSuCoDaKhacPhuc ?? 0,
          lucLuongUngCuu: matched?.lucLuongUngCuu ?? '',
          ghiChu: matched?.ghiChu ?? '',
        });
      }
    }
    return result;
  }

  private toRequest(row: GiamSatSocRow, donViId: number): UpsertGiamSatSocRequest {
    return {
      donViId,
      loaiMang: row.loaiMang,
      lopGiamSat: row.lopGiamSat,
      coHeThong: row.coHeThong,
      thucTrang: row.thucTrang,
      tongSoDoiTuong: row.tongSoDoiTuong,
      soGiamSatMotPhan: row.soGiamSatMotPhan,
      soGiamSatCoBan: row.soGiamSatCoBan,
      soGiamSatDayDu: row.soGiamSatDayDu,
      soSuCo: row.soSuCo,
      soSuCoDaKhacPhuc: row.soSuCoDaKhacPhuc,
      lucLuongUngCuu: row.lucLuongUngCuu.trim() || null,
      ghiChu: row.ghiChu.trim() || null,
    };
  }
}
