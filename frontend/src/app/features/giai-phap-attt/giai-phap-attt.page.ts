import { CommonModule } from '@angular/common';
import { Component, computed, signal } from '@angular/core';
import {
  FormBuilder,
  FormsModule,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { TableModule } from 'primeng/table';
import { CodesApi, CodeValueDto } from '../codes/codes.api';
import { NotificationService } from '../../core/ui/notification.service';
import { FilterBarComponent } from '../../shared/ui/filter-bar.component';
import { FormActionBarComponent } from '../../shared/ui/form-action-bar.component';
import { LoadingOverlayComponent } from '../../shared/ui/loading-overlay.component';
import { SectionCardComponent } from '../../shared/ui/section-card.component';
import {
  GiaiPhapAtttApi,
  GiaiPhapAtttDto,
  SaveGiaiPhapAtttMatrixRequest,
  UpsertGiaiPhapAtttRequest,
} from './giai-phap-attt.api';

interface GiaiPhapOption {
  label: string;
  value: string;
}

interface GiaiPhapAtttRow {
  localId: string;
  id: number | null;
  donViId: number;
  tenGiaiPhap: string;
  mayTinhBcanetSl: number | null;
  mayTinhBcanetTs: number | null;
  mayTinhInternetSl: number | null;
  mayTinhInternetTs: number | null;
  mayTinhLocalSl: number | null;
  mayTinhLocalTs: number | null;
  mayChuBcanetSl: number | null;
  mayChuBcanetTs: number | null;
  mayChuInternetSl: number | null;
  mayChuInternetTs: number | null;
  mayChuLocalSl: number | null;
  mayChuLocalTs: number | null;
  ghiChu: string;
}

@Component({
  selector: 'app-giai-phap-attt-page',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    SectionCardComponent,
    FilterBarComponent,
    FormActionBarComponent,
    LoadingOverlayComponent,
    InputNumberModule,
    InputTextModule,
    ButtonModule,
    TableModule,
  ],
  templateUrl: './giai-phap-attt.page.html',
  styleUrl: './giai-phap-attt.page.scss',
})
export class GiaiPhapAtttPage {
  readonly filterForm = this.formBuilder.group({
    donViId: [2002, [Validators.required]],
  });

  readonly giaiPhapValues = signal<CodeValueDto[]>([]);
  readonly rows = signal<GiaiPhapAtttRow[]>([]);
  readonly loading = signal(false);
  readonly saving = signal(false);

  readonly giaiPhapOptions = computed<GiaiPhapOption[]>(() =>
    this.giaiPhapValues().map((item) => ({
      label: item.name,
      value: item.value,
    })),
  );

  constructor(
    private readonly formBuilder: FormBuilder,
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
    if (this.filterForm.invalid) {
      this.filterForm.markAllAsTouched();
      return;
    }

    const donViId = Number(this.filterForm.controls.donViId.value ?? 0);
    this.loading.set(true);
    try {
      const items = await this.giaiPhapAtttApi.getAll({ donViId });
      this.rows.set(this.buildRows(donViId, items));
    } finally {
      this.loading.set(false);
    }
  }

  async save(): Promise<void> {
    if (this.filterForm.invalid || this.saving()) {
      this.filterForm.markAllAsTouched();
      return;
    }

    const donViId = Number(this.filterForm.controls.donViId.value ?? 0);
    const payload: SaveGiaiPhapAtttMatrixRequest = {
      donViId,
      items: this.rows().map((row) => this.toRequest(row, donViId)),
    };

    this.saving.set(true);
    try {
      const savedItems = await this.giaiPhapAtttApi.saveMatrix(payload);
      this.rows.set(this.buildRows(donViId, savedItems));
      this.notificationService.show(
        'success',
        'Luu ma tran giai phap ATTT thanh cong.',
      );
    } finally {
      this.saving.set(false);
    }
  }

  trackByRow(index: number, row: GiaiPhapAtttRow): string {
    return row.localId || `${index}`;
  }

  resolveGiaiPhapLabel(value: string): string {
    return (
      this.giaiPhapOptions().find((item) => item.value === value)?.label ??
      value
    );
  }

  formatPercent(numerator: number | null, denominator: number | null): string {
    const soLuong = numerator ?? 0;
    const tongSo = denominator ?? 0;
    if (tongSo <= 0) {
      return '-';
    }

    return `${((soLuong / tongSo) * 100).toFixed(1)}%`;
  }

  private buildRows(
    donViId: number,
    items: GiaiPhapAtttDto[],
  ): GiaiPhapAtttRow[] {
    return this.giaiPhapValues().map((item) => {
      const existing = items.find((entry) => entry.tenGiaiPhap === item.value);
      return {
        localId: item.value,
        id: existing?.id ?? null,
        donViId,
        tenGiaiPhap: item.value,
        mayTinhBcanetSl: existing?.mayTinhBcanetSl ?? 0,
        mayTinhBcanetTs: existing?.mayTinhBcanetTs ?? 0,
        mayTinhInternetSl: existing?.mayTinhInternetSl ?? 0,
        mayTinhInternetTs: existing?.mayTinhInternetTs ?? 0,
        mayTinhLocalSl: existing?.mayTinhLocalSl ?? 0,
        mayTinhLocalTs: existing?.mayTinhLocalTs ?? 0,
        mayChuBcanetSl: existing?.mayChuBcanetSl ?? 0,
        mayChuBcanetTs: existing?.mayChuBcanetTs ?? 0,
        mayChuInternetSl: existing?.mayChuInternetSl ?? 0,
        mayChuInternetTs: existing?.mayChuInternetTs ?? 0,
        mayChuLocalSl: existing?.mayChuLocalSl ?? 0,
        mayChuLocalTs: existing?.mayChuLocalTs ?? 0,
        ghiChu: existing?.ghiChu ?? '',
      };
    });
  }

  private toRequest(
    row: GiaiPhapAtttRow,
    donViId: number,
  ): UpsertGiaiPhapAtttRequest {
    return {
      donViId,
      tenGiaiPhap: row.tenGiaiPhap,
      mayTinhBcanetSl: row.mayTinhBcanetSl ?? 0,
      mayTinhBcanetTs: row.mayTinhBcanetTs ?? 0,
      mayTinhInternetSl: row.mayTinhInternetSl ?? 0,
      mayTinhInternetTs: row.mayTinhInternetTs ?? 0,
      mayTinhLocalSl: row.mayTinhLocalSl ?? 0,
      mayTinhLocalTs: row.mayTinhLocalTs ?? 0,
      mayChuBcanetSl: row.mayChuBcanetSl ?? 0,
      mayChuBcanetTs: row.mayChuBcanetTs ?? 0,
      mayChuInternetSl: row.mayChuInternetSl ?? 0,
      mayChuInternetTs: row.mayChuInternetTs ?? 0,
      mayChuLocalSl: row.mayChuLocalSl ?? 0,
      mayChuLocalTs: row.mayChuLocalTs ?? 0,
      ghiChu: row.ghiChu.trim() || null,
    };
  }
}
