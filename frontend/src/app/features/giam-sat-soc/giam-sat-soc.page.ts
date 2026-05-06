import { CommonModule } from '@angular/common';
import { Component, computed, signal } from '@angular/core';
import {
  FormsModule,
  ReactiveFormsModule,
  FormBuilder,
  Validators,
} from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { DropdownModule } from 'primeng/dropdown';
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
  donViId: number;
  loaiMang: string;
  lopGiamSat: string;
  coHeThong: boolean;
  thucTrang: string | null;
  namThanhLap: number | null;
  soNhanSu: number | null;
  congCuSuDung: string;
  soCanhBaoThang: number | null;
  ghiChu: string;
}

@Component({
  selector: 'app-giam-sat-soc-page',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    SectionCardComponent,
    FilterBarComponent,
    FormActionBarComponent,
    LoadingOverlayComponent,
    DropdownModule,
    CheckboxModule,
    InputNumberModule,
    InputTextModule,
    ButtonModule,
    TableModule,
  ],
  templateUrl: './giam-sat-soc.page.html',
  styleUrl: './giam-sat-soc.page.scss',
})
export class GiamSatSocPage {
  readonly filterForm = this.formBuilder.group({
    donViId: [2002, [Validators.required]],
  });

  readonly loaiMangValues = signal<CodeValueDto[]>([]);
  readonly lopGiamSatValues = signal<CodeValueDto[]>([]);
  readonly thucTrangValues = signal<CodeValueDto[]>([]);
  readonly rows = signal<GiamSatSocRow[]>([]);
  readonly loading = signal(false);
  readonly saving = signal(false);

  readonly loaiMangOptions = computed<SelectOption[]>(() =>
    this.loaiMangValues().map((item) => ({
      label: item.name,
      value: item.value,
    })),
  );

  readonly lopGiamSatOptions = computed<SelectOption[]>(() =>
    this.lopGiamSatValues().map((item) => ({
      label: item.name,
      value: item.value,
    })),
  );

  readonly thucTrangOptions = computed<SelectOption[]>(() =>
    this.thucTrangValues().map((item) => ({
      label: item.name,
      value: item.value,
    })),
  );

  constructor(
    private readonly formBuilder: FormBuilder,
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
    if (this.filterForm.invalid) {
      this.filterForm.markAllAsTouched();
      return;
    }

    const donViId = Number(this.filterForm.controls.donViId.value ?? 0);

    this.loading.set(true);
    try {
      const items = await this.giamSatSocApi.getAll({ donViId });
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
    const payload: SaveGiamSatSocMatrixRequest = {
      donViId,
      items: this.rows().map((row) => this.toRequest(row, donViId)),
    };

    this.saving.set(true);
    try {
      const savedItems = await this.giamSatSocApi.saveMatrix(payload);
      this.rows.set(this.buildRows(donViId, savedItems));
      this.notificationService.show(
        'success',
        'Luu ma tran giam sat SOC thanh cong.',
      );
    } finally {
      this.saving.set(false);
    }
  }

  onCoHeThongChange(loaiMang: string, checked: boolean): void {
    this.rows.update((items) =>
      items.map((item) =>
        item.loaiMang === loaiMang ? { ...item, coHeThong: checked } : item,
      ),
    );
  }

  resolveLoaiMangLabel(value: string): string {
    return (
      this.loaiMangOptions().find((item) => item.value === value)?.label ??
      value
    );
  }

  resolveLopGiamSatLabel(value: string): string {
    return (
      this.lopGiamSatOptions().find((item) => item.value === value)?.label ??
      value
    );
  }

  trackByRow(index: number, row: GiamSatSocRow): string {
    return row.localId || `${index}`;
  }

  private buildRows(donViId: number, items: GiamSatSocDto[]): GiamSatSocRow[] {
    const result: GiamSatSocRow[] = [];

    for (const loaiMang of this.loaiMangValues()) {
      const groupItems = items.filter(
        (item) => item.loaiMang === loaiMang.value,
      );
      const coHeThong = groupItems[0]?.coHeThong ?? false;

      for (const lopGiamSat of this.lopGiamSatValues()) {
        const matched = groupItems.find(
          (item) => item.lopGiamSat === lopGiamSat.value,
        );
        result.push({
          localId: `${loaiMang.value}-${lopGiamSat.value}`,
          id: matched?.id ?? null,
          donViId,
          loaiMang: loaiMang.value,
          lopGiamSat: lopGiamSat.value,
          coHeThong,
          thucTrang: matched?.thucTrang ?? null,
          namThanhLap: matched?.namThanhLap ?? null,
          soNhanSu: matched?.soNhanSu ?? null,
          congCuSuDung: matched?.congCuSuDung ?? '',
          soCanhBaoThang: matched?.soCanhBaoThang ?? null,
          ghiChu: matched?.ghiChu ?? '',
        });
      }
    }

    return result;
  }

  private toRequest(
    row: GiamSatSocRow,
    donViId: number,
  ): UpsertGiamSatSocRequest {
    return {
      donViId,
      loaiMang: row.loaiMang,
      lopGiamSat: row.lopGiamSat,
      coHeThong: row.coHeThong,
      thucTrang: row.thucTrang,
      namThanhLap: row.namThanhLap,
      soNhanSu: row.soNhanSu,
      congCuSuDung: this.normalizeText(row.congCuSuDung),
      soCanhBaoThang: row.soCanhBaoThang,
      ghiChu: this.normalizeText(row.ghiChu),
    };
  }

  private normalizeText(value: string | null | undefined): string | null {
    const normalized = value?.trim();
    return normalized ? normalized : null;
  }
}
