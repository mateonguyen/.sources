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
  donViId: number;
  lopGiamSat: string;
  coNoc: boolean;
  thucTrang: string | null;
  namThanhLap: number | null;
  soNhanSu: number | null;
  ghiChu: string;
}

@Component({
  selector: 'app-giam-sat-noc-page',
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
  templateUrl: './giam-sat-noc.page.html',
  styleUrl: './giam-sat-noc.page.scss',
})
export class GiamSatNocPage {
  readonly filterForm = this.formBuilder.group({
    donViId: [2002, [Validators.required]],
  });

  readonly lopGiamSatValues = signal<CodeValueDto[]>([]);
  readonly thucTrangValues = signal<CodeValueDto[]>([]);
  readonly rows = signal<GiamSatNocRow[]>([]);
  readonly loading = signal(false);
  readonly saving = signal(false);

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
    if (this.filterForm.invalid) {
      this.filterForm.markAllAsTouched();
      return;
    }

    const donViId = Number(this.filterForm.controls.donViId.value ?? 0);

    this.loading.set(true);
    try {
      const items = await this.giamSatNocApi.getAll({ donViId });
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
    const payload: SaveGiamSatNocMatrixRequest = {
      donViId,
      items: this.rows().map((row) => this.toRequest(row, donViId)),
    };

    this.saving.set(true);
    try {
      const savedItems = await this.giamSatNocApi.saveMatrix(payload);
      this.rows.set(this.buildRows(donViId, savedItems));
      this.notificationService.show(
        'success',
        'Luu ma tran giam sat NOC thanh cong.',
      );
    } finally {
      this.saving.set(false);
    }
  }

  onCoNocChange(checked: boolean): void {
    this.rows.update((items) =>
      items.map((item) => ({ ...item, coNoc: checked })),
    );
  }

  resolveLopGiamSatLabel(value: string): string {
    return (
      this.lopGiamSatOptions().find((item) => item.value === value)?.label ??
      value
    );
  }

  trackByRow(index: number, row: GiamSatNocRow): string {
    return row.localId || `${index}`;
  }

  private buildRows(donViId: number, items: GiamSatNocDto[]): GiamSatNocRow[] {
    const coNoc = items[0]?.coNoc ?? false;

    return this.lopGiamSatValues().map((lopGiamSat) => {
      const matched = items.find(
        (item) => item.lopGiamSat === lopGiamSat.value,
      );
      return {
        localId: lopGiamSat.value,
        id: matched?.id ?? null,
        donViId,
        lopGiamSat: lopGiamSat.value,
        coNoc,
        thucTrang: matched?.thucTrang ?? null,
        namThanhLap: matched?.namThanhLap ?? null,
        soNhanSu: matched?.soNhanSu ?? null,
        ghiChu: matched?.ghiChu ?? '',
      };
    });
  }

  private toRequest(
    row: GiamSatNocRow,
    donViId: number,
  ): UpsertGiamSatNocRequest {
    return {
      donViId,
      lopGiamSat: row.lopGiamSat,
      coNoc: row.coNoc,
      thucTrang: row.thucTrang,
      namThanhLap: row.namThanhLap,
      soNhanSu: row.soNhanSu,
      ghiChu: this.normalizeText(row.ghiChu),
    };
  }

  private normalizeText(value: string | null | undefined): string | null {
    const normalized = value?.trim();
    return normalized ? normalized : null;
  }
}
