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
import { TongHopModeBannerComponent } from '../../shared/ui/tong-hop-mode-banner.component';
import {
  GiamSatNocApi,
  GiamSatNocDto,
  SaveGiamSatNocMatrixRequest,
  UpsertGiamSatNocRequest,
} from './giam-sat-noc.api';

interface SelectOption {
  label: string;
  value: string;
}

interface GiamSatNocRow {
  localId: string;
  id: number | null;
  lopGiamSat: string;
  lopGiamSatLabel: string;
  coNoc: boolean;
  thucTrang: string | null;
  tongSoDoiTuong: number;
  soDaGiamSat: number;
  ghiChu: string;
}

@Component({
  selector: 'app-giam-sat-noc-page',
  standalone: true,
  imports: [
    TongHopModeBannerComponent,
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
  templateUrl: './giam-sat-noc.page.html',
  styleUrl: './giam-sat-noc.page.scss',
})
export class GiamSatNocPage {
  readonly donViId = computed(() => this.authService.profile()?.donViId ?? 0);

  readonly lopGiamSatValues = signal<CodeValueDto[]>([]);
  readonly thucTrangValues = signal<CodeValueDto[]>([]);
  readonly rows = signal<GiamSatNocRow[]>([]);
  readonly loading = signal(false);
  readonly saving = signal(false);

  readonly coNoc = computed(() => this.rows()[0]?.coNoc ?? false);

  readonly thucTrangOptions = computed<SelectOption[]>(() =>
    this.thucTrangValues().map((item) => ({
      label: item.name,
      value: item.value,
    })),
  );

  readonly hasValidationError = computed(() =>
    this.rows().some((r) => r.soDaGiamSat > r.tongSoDoiTuong),
  );

  readonly totalTongSoDoiTuong = computed(() =>
    this.rows().reduce((sum, r) => sum + r.tongSoDoiTuong, 0),
  );

  readonly totalSoDaGiamSat = computed(() =>
    this.rows().reduce((sum, r) => sum + r.soDaGiamSat, 0),
  );

  constructor(
    private readonly authService: AuthService,
    private readonly giamSatNocApi: GiamSatNocApi,
    private readonly codesApi: CodesApi,
    private readonly notificationService: NotificationService,
  ) {
    void this.initialize();
  }

  async initialize(): Promise<void> {
    this.loading.set(true);
    try {
      const [lopGiamSatCode, thucTrangCode] = await Promise.all([
        this.codesApi.getByCode('LOP_GIAM_SAT'),
        this.codesApi.getByCode('THUC_TRANG_GIAM_SAT'),
      ]);
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
      const items = await this.giamSatNocApi.getAll({ donViId });
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
    const payload: SaveGiamSatNocMatrixRequest = {
      donViId,
      items: this.rows().map((row) => this.toRequest(row, donViId)),
    };
    this.saving.set(true);
    try {
      const savedItems = await this.giamSatNocApi.saveMatrix(payload);
      this.rows.set(this.buildRows(savedItems));
      this.notificationService.show('success', 'Lưu ma trận giám sát NOC thành công.');
    } finally {
      this.saving.set(false);
    }
  }

  onCoNocChange(checked: boolean): void {
    this.rows.update((items) => items.map((item) => ({ ...item, coNoc: checked })));
  }

  hasRowError(row: GiamSatNocRow): boolean {
    return row.soDaGiamSat > row.tongSoDoiTuong;
  }

  trackByRow(_: number, row: GiamSatNocRow): string {
    return row.localId;
  }

  private buildRows(items: GiamSatNocDto[]): GiamSatNocRow[] {
    const coNoc = items[0]?.coNoc ?? false;
    return this.lopGiamSatValues().map((lop) => {
      const matched = items.find((item) => item.lopGiamSat === lop.value);
      return {
        localId: lop.value,
        id: matched?.id ?? null,
        lopGiamSat: lop.value,
        lopGiamSatLabel: lop.name,
        coNoc,
        thucTrang: matched?.thucTrang ?? null,
        tongSoDoiTuong: matched?.tongSoDoiTuong ?? 0,
        soDaGiamSat: matched?.soDaGiamSat ?? 0,
        ghiChu: matched?.ghiChu ?? '',
      };
    });
  }

  private toRequest(row: GiamSatNocRow, donViId: number): UpsertGiamSatNocRequest {
    return {
      donViId,
      lopGiamSat: row.lopGiamSat,
      coNoc: row.coNoc,
      thucTrang: row.thucTrang,
      tongSoDoiTuong: row.tongSoDoiTuong,
      soDaGiamSat: row.soDaGiamSat,
      ghiChu: row.ghiChu.trim() || null,
    };
  }
}
