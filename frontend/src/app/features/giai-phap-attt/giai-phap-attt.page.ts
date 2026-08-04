import { CommonModule } from '@angular/common';
import { Component, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DropdownModule } from 'primeng/dropdown';
import { AuthService } from '../../core/auth/auth.service';
import { NotificationService } from '../../core/ui/notification.service';
import { LoadingOverlayComponent } from '../../shared/ui/loading-overlay.component';
import { SectionCardComponent } from '../../shared/ui/section-card.component';
import { CodesApi, CodeValueDto } from '../codes/codes.api';
import { TongHopModeBannerComponent } from '../../shared/ui/tong-hop-mode-banner.component';
import {
  GiaiPhapAtttApi,
  GiaiPhapAtttDto,
  SaveGiaiPhapAtttMatrixRequest,
  UpsertGiaiPhapAtttRequest,
} from './giai-phap-attt.api';

type MetricField =
  | 'mayTinhBcanet'
  | 'mayTinhInternet'
  | 'mayTinhLocal'
  | 'mayChuBcanet'
  | 'mayChuInternet'
  | 'mayChuLocal';

type MetricPart = 'Sl' | 'Ts';

type NumericField =
  | 'mayTinhBcanetSl'
  | 'mayTinhBcanetTs'
  | 'mayTinhInternetSl'
  | 'mayTinhInternetTs'
  | 'mayTinhLocalSl'
  | 'mayTinhLocalTs'
  | 'mayChuBcanetSl'
  | 'mayChuBcanetTs'
  | 'mayChuInternetSl'
  | 'mayChuInternetTs'
  | 'mayChuLocalSl'
  | 'mayChuLocalTs';

interface GiaiPhapAtttRow {
  localId: string;
  id: number | null;
  tenGiaiPhap: string;
  mayTinhBcanetSl: number;
  mayTinhBcanetTs: number;
  mayTinhInternetSl: number;
  mayTinhInternetTs: number;
  mayTinhLocalSl: number;
  mayTinhLocalTs: number;
  mayChuBcanetSl: number;
  mayChuBcanetTs: number;
  mayChuInternetSl: number;
  mayChuInternetTs: number;
  mayChuLocalSl: number;
  mayChuLocalTs: number;
  ghiChu: string;
  dirty?: boolean;
}

@Component({
  selector: 'app-giai-phap-attt-page',
  standalone: true,
  imports: [
    TongHopModeBannerComponent,
    CommonModule,
    FormsModule,
    SectionCardComponent,
    LoadingOverlayComponent,
    ButtonModule,
    DropdownModule,
  ],
  templateUrl: './giai-phap-attt.page.html',
  styleUrl: './giai-phap-attt.page.scss',
})
export class GiaiPhapAtttPage {
  readonly donViId = computed(() => this.authService.profile()?.donViId ?? 0);
  readonly giaiPhapValues = signal<CodeValueDto[]>([]);
  readonly rows = signal<GiaiPhapAtttRow[]>([]);
  readonly selectedGiaiPhapToAdd = signal<string | null>(null);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly matrixDirty = signal(false);
  private rowSeed = 0;

  readonly metricGroups: ReadonlyArray<{
    group: 'MÁY TÍNH' | 'MÁY CHỦ';
    tone: 'computer' | 'server';
    items: ReadonlyArray<{ label: string; field: MetricField }>;
  }> = [
    {
      group: 'MÁY TÍNH',
      tone: 'computer',
      items: [
        { label: 'BCANet', field: 'mayTinhBcanet' },
        { label: 'Internet', field: 'mayTinhInternet' },
        { label: 'Local/Độc lập', field: 'mayTinhLocal' },
      ],
    },
    {
      group: 'MÁY CHỦ',
      tone: 'server',
      items: [
        { label: 'BCANet', field: 'mayChuBcanet' },
        { label: 'Internet', field: 'mayChuInternet' },
        { label: 'Local', field: 'mayChuLocal' },
      ],
    },
  ];

  get hasDirtyRows(): boolean {
    return this.matrixDirty() || this.rows().some((r) => !!r.dirty);
  }

  readonly availableGiaiPhapOptions = computed(() => {
    const used = new Set(this.rows().map((row) => row.tenGiaiPhap));
    return this.giaiPhapValues()
      .filter((item) => !used.has(item.value))
      .map((item) => ({
        label: item.name,
        value: item.value,
      }));
  });

  readonly hasAvailableGiaiPhapToAdd = computed(
    () => this.availableGiaiPhapOptions().length > 0,
  );

  constructor(
    private readonly authService: AuthService,
    private readonly giaiPhapAtttApi: GiaiPhapAtttApi,
    private readonly codesApi: CodesApi,
    private readonly notificationService: NotificationService,
  ) {
    void this.initialize();
  }

  async initialize(): Promise<void> {
    this.loading.set(true);
    try {
      const code = await this.codesApi.getByCode('GIAI_PHAP_ATTT');
      this.giaiPhapValues.set(code.values.filter((item) => item.isActive));
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
      const items = await this.giaiPhapAtttApi.getAll({ donViId });
      this.rows.set(this.buildRows(items));
      this.matrixDirty.set(false);
      this.selectedGiaiPhapToAdd.set(null);
    } finally {
      this.loading.set(false);
    }
  }

  async save(): Promise<void> {
    if (this.saving() || !this.hasDirtyRows) return;

    const donViId = this.donViId();
    if (!donViId) {
      this.notificationService.show(
        'error',
        'Không xác định được đơn vị hiện tại.',
      );
      return;
    }

    const rows = this.rows();
    const missingName = rows.find((row) => !row.tenGiaiPhap.trim());
    if (missingName) {
      this.notificationService.show(
        'warning',
        'Tên giải pháp không được để trống.',
      );
      return;
    }

    const duplicate = this.findDuplicateTenGiaiPhap(
      rows.map((row) => row.tenGiaiPhap),
    );
    if (duplicate) {
      this.notificationService.show(
        'warning',
        'Danh sách có giải pháp bị trùng. Vui lòng chọn lại.',
      );
      return;
    }

    const payload: SaveGiaiPhapAtttMatrixRequest = {
      donViId,
      items: rows.map((row) => this.toRequest(row, donViId)),
    };

    this.saving.set(true);
    try {
      const savedItems = await this.giaiPhapAtttApi.saveMatrix(payload);
      this.rows.set(this.buildRows(savedItems));
      this.matrixDirty.set(false);
      this.selectedGiaiPhapToAdd.set(null);
      this.notificationService.show(
        'success',
        'Lưu ma trận giải pháp ATTT thành công.',
      );
    } finally {
      this.saving.set(false);
    }
  }

  getTenGiaiPhapOptions(row: GiaiPhapAtttRow): Array<{
    label: string;
    value: string;
    disabled?: boolean;
  }> {
    const usedByOtherRows = new Set(
      this.rows()
        .filter((item) => item.localId !== row.localId)
        .map((item) => item.tenGiaiPhap),
    );

    const options = this.giaiPhapValues().map((item) => ({
      label: item.name,
      value: item.value,
      disabled: usedByOtherRows.has(item.value),
    }));

    if (
      row.tenGiaiPhap &&
      !options.some((opt) => opt.value === row.tenGiaiPhap)
    ) {
      options.unshift({
        label: row.tenGiaiPhap,
        value: row.tenGiaiPhap,
        disabled: false,
      });
    }

    return options;
  }

  onSelectedGiaiPhapToAddChange(value: string | null): void {
    this.selectedGiaiPhapToAdd.set(value ?? null);
  }

  addRowFromCatalog(): void {
    const selected = this.selectedGiaiPhapToAdd();
    if (!selected) {
      this.notificationService.show('info', 'Vui lòng chọn giải pháp để thêm.');
      return;
    }

    const exists = this.rows().some((row) => row.tenGiaiPhap === selected);
    if (exists) {
      this.notificationService.show(
        'warning',
        'Giải pháp này đã tồn tại trong bảng.',
      );
      return;
    }

    const donViId = this.donViId();
    if (!donViId) return;

    this.rows.set([
      ...this.rows(),
      this.createEmptyRow(selected, null, `gpattt-new-${this.rowSeed++}`),
    ]);
    this.selectedGiaiPhapToAdd.set(null);
    this.matrixDirty.set(true);
  }

  addFirstAvailableRow(): void {
    const first = this.availableGiaiPhapOptions()[0]?.value;
    if (!first) {
      this.notificationService.show('info', 'Không còn giải pháp nào để thêm.');
      return;
    }

    this.selectedGiaiPhapToAdd.set(first);
    this.addRowFromCatalog();
  }

  removeRow(row: GiaiPhapAtttRow): void {
    this.rows.set(this.rows().filter((item) => item.localId !== row.localId));
    this.matrixDirty.set(true);
  }

  onTenGiaiPhapChange(row: GiaiPhapAtttRow, nextValue: string | null): void {
    const value = (nextValue ?? '').trim();
    if (!value || value === row.tenGiaiPhap) return;

    const duplicated = this.rows().some(
      (item) => item.localId !== row.localId && item.tenGiaiPhap === value,
    );
    if (duplicated) {
      this.notificationService.show('warning', 'Giải pháp đã tồn tại.');
      return;
    }

    this.rows.set(
      this.rows().map((item) =>
        item.localId === row.localId
          ? {
              ...item,
              tenGiaiPhap: value,
              dirty: true,
            }
          : item,
      ),
    );
    this.matrixDirty.set(true);
  }

  trackByRow(_: number, row: GiaiPhapAtttRow): string {
    return row.localId;
  }

  getPercent(row: GiaiPhapAtttRow, field: MetricField): string {
    const sl = this.getMetricValue(row, field, 'Sl');
    const ts = this.getMetricValue(row, field, 'Ts');
    if (ts <= 0) return '0%';
    return `${Math.round((sl / ts) * 100)}%`;
  }

  getPercentClass(row: GiaiPhapAtttRow, field: MetricField): string {
    const sl = this.getMetricValue(row, field, 'Sl');
    const ts = this.getMetricValue(row, field, 'Ts');
    if (ts <= 0) return 'pct--zero';
    const pct = (sl / ts) * 100;
    if (pct >= 80) return 'pct--high';
    if (pct >= 50) return 'pct--mid';
    return 'pct--low';
  }

  onInputEnter(event: Event, rowIdx: number, colIdx: number): void {
    event.preventDefault();
    const totalCols = 12;
    const nextColIdx = colIdx + 1 >= totalCols ? 0 : colIdx + 1;
    const nextRowIdx = colIdx + 1 >= totalCols ? rowIdx + 1 : rowIdx;
    const next = document.getElementById(
      `gp-${nextRowIdx}-${nextColIdx}`,
    ) as HTMLInputElement | null;
    if (next) {
      next.focus();
      next.select();
    }
  }

  onMetricChange(
    row: GiaiPhapAtttRow,
    field: MetricField,
    part: MetricPart,
    value: string,
  ): void {
    const numericField = `${field}${part}` as NumericField;
    const nextValue = this.sanitizeNumber(value);

    this.rows.set(
      this.rows().map((item) =>
        item.localId === row.localId
          ? {
              ...item,
              [numericField]: nextValue,
              dirty: true,
            }
          : item,
      ),
    );
    this.matrixDirty.set(true);
  }

  onNoteChange(row: GiaiPhapAtttRow, value: string): void {
    this.rows.set(
      this.rows().map((item) =>
        item.localId === row.localId
          ? {
              ...item,
              ghiChu: value,
              dirty: true,
            }
          : item,
      ),
    );
    this.matrixDirty.set(true);
  }

  onNoteInput(row: GiaiPhapAtttRow, value: string, event: Event): void {
    this.onNoteChange(row, value);
    const target = event.target as HTMLTextAreaElement | null;
    if (!target) return;

    target.style.height = 'auto';
    target.style.height = `${Math.min(target.scrollHeight, 88)}px`;
  }

  private getMetricValue(
    row: GiaiPhapAtttRow,
    field: MetricField,
    part: MetricPart,
  ): number {
    const key = `${field}${part}` as NumericField;
    return row[key] ?? 0;
  }

  private sanitizeNumber(value: string): number {
    const parsed = Number(value);
    if (!Number.isFinite(parsed) || parsed < 0) return 0;
    return Math.trunc(parsed);
  }

  private buildRows(items: GiaiPhapAtttDto[]): GiaiPhapAtttRow[] {
    const order = new Map(
      this.giaiPhapValues().map((item, idx) => [item.value.toUpperCase(), idx]),
    );

    return [...items]
      .sort((a, b) => {
        const ai =
          order.get(a.tenGiaiPhap.toUpperCase()) ?? Number.MAX_SAFE_INTEGER;
        const bi =
          order.get(b.tenGiaiPhap.toUpperCase()) ?? Number.MAX_SAFE_INTEGER;
        if (ai === bi) {
          return a.tenGiaiPhap.localeCompare(b.tenGiaiPhap);
        }
        return ai - bi;
      })
      .map((item) =>
        this.createEmptyRow(item.tenGiaiPhap, item, `gpattt-${item.id}`),
      );
  }

  private createEmptyRow(
    tenGiaiPhap: string,
    source: GiaiPhapAtttDto | null,
    localId: string,
  ): GiaiPhapAtttRow {
    return {
      localId,
      id: source?.id ?? null,
      tenGiaiPhap,
      mayTinhBcanetSl: source?.mayTinhBcanetSl ?? 0,
      mayTinhBcanetTs: source?.mayTinhBcanetTs ?? 0,
      mayTinhInternetSl: source?.mayTinhInternetSl ?? 0,
      mayTinhInternetTs: source?.mayTinhInternetTs ?? 0,
      mayTinhLocalSl: source?.mayTinhLocalSl ?? 0,
      mayTinhLocalTs: source?.mayTinhLocalTs ?? 0,
      mayChuBcanetSl: source?.mayChuBcanetSl ?? 0,
      mayChuBcanetTs: source?.mayChuBcanetTs ?? 0,
      mayChuInternetSl: source?.mayChuInternetSl ?? 0,
      mayChuInternetTs: source?.mayChuInternetTs ?? 0,
      mayChuLocalSl: source?.mayChuLocalSl ?? 0,
      mayChuLocalTs: source?.mayChuLocalTs ?? 0,
      ghiChu: source?.ghiChu ?? '',
      dirty: source ? false : true,
    };
  }

  private findDuplicateTenGiaiPhap(values: string[]): string | null {
    const seen = new Set<string>();
    for (const value of values) {
      const key = value.trim().toUpperCase();
      if (!key) continue;
      if (seen.has(key)) return value;
      seen.add(key);
    }
    return null;
  }

  private toRequest(
    row: GiaiPhapAtttRow,
    donViId: number,
  ): UpsertGiaiPhapAtttRequest {
    return {
      donViId,
      tenGiaiPhap: row.tenGiaiPhap,
      mayTinhBcanetSl: row.mayTinhBcanetSl,
      mayTinhBcanetTs: row.mayTinhBcanetTs,
      mayTinhInternetSl: row.mayTinhInternetSl,
      mayTinhInternetTs: row.mayTinhInternetTs,
      mayTinhLocalSl: row.mayTinhLocalSl,
      mayTinhLocalTs: row.mayTinhLocalTs,
      mayChuBcanetSl: row.mayChuBcanetSl,
      mayChuBcanetTs: row.mayChuBcanetTs,
      mayChuInternetSl: row.mayChuInternetSl,
      mayChuInternetTs: row.mayChuInternetTs,
      mayChuLocalSl: row.mayChuLocalSl,
      mayChuLocalTs: row.mayChuLocalTs,
      ghiChu: row.ghiChu.trim() || null,
    };
  }
}
