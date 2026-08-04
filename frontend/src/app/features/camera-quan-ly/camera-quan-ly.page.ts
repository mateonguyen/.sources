import { CommonModule } from '@angular/common';
import { Component, computed, signal } from '@angular/core';
import {
  FormBuilder,
  FormsModule,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { DialogModule } from 'primeng/dialog';
import { DropdownModule } from 'primeng/dropdown';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { InputTextareaModule } from 'primeng/inputtextarea';
import { TableModule } from 'primeng/table';
import { TooltipModule } from 'primeng/tooltip';
import { AuthService } from '../../core/auth/auth.service';
import { NotificationService } from '../../core/ui/notification.service';
import { ConfirmDialogWrapperService } from '../../shared/ui/confirm-dialog-wrapper.service';
import { EmptyStateComponent } from '../../shared/ui/empty-state.component';
import { LoadingOverlayComponent } from '../../shared/ui/loading-overlay.component';
import { SectionCardComponent } from '../../shared/ui/section-card.component';
import {
  APP_SELECT_PANEL_STYLE_CLASS,
  APP_TABLE_BODY_CELL_CLASS,
  APP_TABLE_HEADER_CELL_CLASS,
  APP_TABLE_ROW_CLASS,
  APP_TABLE_STYLE_CLASS,
} from '../../shared/ui/primeng-pt';
import { CodesApi } from '../codes/codes.api';
import { TongHopModeBannerComponent } from '../../shared/ui/tong-hop-mode-banner.component';
import {
  CameraQuanLyApi,
  CameraQuanLyDto,
  UpsertCameraQuanLyRequest,
} from './camera-quan-ly.api';

type CameraTabKey = 'NHOM_1' | 'NHOM_2';

interface SelectOption<T = string> {
  label: string;
  value: T;
}

type CameraFormControlName =
  | 'nhomCamera'
  | 'tenDonViDiaChi'
  | 'buongGiamTrangBiSl'
  | 'buongGiamTrangBiTs'
  | 'nhuCauDauTu'
  | 'baoTri'
  | 'suaChua'
  | 'soLanViPham'
  | 'ketNoiChiaSe'
  | 'hoSoCapDoAttt'
  | 'cbChuyenTrach'
  | 'cbKiemNhiem'
  | 'cbDiaPhuong'
  | 'daoTaoBo'
  | 'daoTaoNhuCau'
  | 'ghiChu';

@Component({
  selector: 'app-camera-quan-ly-page',
  standalone: true,
  imports: [
    TongHopModeBannerComponent,
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    SectionCardComponent,
    EmptyStateComponent,
    LoadingOverlayComponent,
    DialogModule,
    DropdownModule,
    CheckboxModule,
    InputNumberModule,
    InputTextModule,
    InputTextareaModule,
    ButtonModule,
    TableModule,
    TooltipModule,
  ],
  templateUrl: './camera-quan-ly.page.html',
  styleUrl: './camera-quan-ly.page.scss',
})
export class CameraQuanLyPage {
  readonly selectPanelStyleClass = APP_SELECT_PANEL_STYLE_CLASS;
  readonly tableStyleClass = APP_TABLE_STYLE_CLASS;
  readonly tableHeaderCellClass = APP_TABLE_HEADER_CELL_CLASS;
  readonly tableRowClass = APP_TABLE_ROW_CLASS;
  readonly tableBodyCellClass = APP_TABLE_BODY_CELL_CLASS;

  // No filter group needed - simple live data view

  readonly form = this.fb.group({
    nhomCamera: [null as string | null],
    tenDonViDiaChi: ['', Validators.required],
    buongGiamTrangBiSl: [0, [Validators.required, Validators.min(0)]],
    buongGiamTrangBiTs: [0, [Validators.required, Validators.min(0)]],
    nhuCauDauTu: [0, [Validators.required, Validators.min(0)]],
    baoTri: [0, [Validators.required, Validators.min(0)]],
    suaChua: [0, [Validators.required, Validators.min(0)]],
    soLanViPham: [0, [Validators.required, Validators.min(0)]],
    ketNoiChiaSe: [null as string | null],
    hoSoCapDoAttt: [0, [Validators.required, Validators.min(0)]],
    cbChuyenTrach: [0, [Validators.required, Validators.min(0)]],
    cbKiemNhiem: [0, [Validators.required, Validators.min(0)]],
    cbDiaPhuong: [0, [Validators.required, Validators.min(0)]],
    daoTaoBo: [0, [Validators.required, Validators.min(0)]],
    daoTaoNhuCau: [0, [Validators.required, Validators.min(0)]],
    ghiChu: [null as string | null],
  });

  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly dialogVisible = signal(false);
  readonly items = signal<CameraQuanLyDto[]>([]);
  readonly cameraGroupOptions = signal<SelectOption[]>([]);
  readonly selectedId = signal<number | null>(null);
  readonly activeTab = signal<CameraTabKey>('NHOM_1');
  readonly filterTenDonVi = signal('');

  readonly donViId = computed(() => this.authService.profile()?.donViId ?? 0);

  readonly canEdit = signal(true); // Always editable in live view

  private readonly defaultTabLabels: Record<CameraTabKey, string> = {
    NHOM_1:
      'Hệ thống camera giám sát tại cơ sở giam giữ, giáo dưỡng, cai nghiện, lưu trú',
    NHOM_2: 'Hệ thống camera giám sát bảo đảm an ninh trật tự',
  };

  readonly nhom1Items = computed(() =>
    this.items().filter((item) => item.nhomCamera === 'NHOM_1'),
  );

  readonly nhom2Items = computed(() =>
    this.items().filter((item) => item.nhomCamera === 'NHOM_2'),
  );

  readonly nhom1Count = computed(() => this.nhom1Items().length);
  readonly nhom2Count = computed(() => this.nhom2Items().length);

  readonly activeItems = computed(() => {
    const keyword = this.filterTenDonVi().trim().toLowerCase();
    const tab = this.activeTab();
    const source = tab === 'NHOM_1' ? this.nhom1Items() : this.nhom2Items();

    if (!keyword) {
      return source;
    }

    return source.filter((item) =>
      item.tenDonViDiaChi.toLowerCase().includes(keyword),
    );
  });

  readonly tabLabels = computed<Record<CameraTabKey, string>>(() => {
    const options = this.cameraGroupOptions();
    const map = new Map<string, string>(
      options.map((option) => [option.value, option.label]),
    );

    return {
      NHOM_1: map.get('NHOM_1') || this.defaultTabLabels.NHOM_1,
      NHOM_2: map.get('NHOM_2') || this.defaultTabLabels.NHOM_2,
    };
  });

  constructor(
    private readonly fb: FormBuilder,
    private readonly authService: AuthService,
    private readonly api: CameraQuanLyApi,
    private readonly codesApi: CodesApi,
    private readonly notificationService: NotificationService,
    private readonly confirmDialog: ConfirmDialogWrapperService,
  ) {
    void this.initialize();
  }

  async initialize(): Promise<void> {
    this.loading.set(true);
    try {
      const cameraCodeDto = await this.codesApi
        .getByCode('NHOM_CAMERA')
        .catch(() => null);

      if (cameraCodeDto?.values) {
        this.cameraGroupOptions.set(
          cameraCodeDto.values.map((code) => ({
            label: this.resolveCameraGroupLabel(code.value, code.name),
            value: code.value,
          })),
        );
      }

      await this.load(true);
    } finally {
      this.loading.set(false);
    }
  }

  setActiveTab(tab: CameraTabKey): void {
    this.activeTab.set(tab);
    this.filterTenDonVi.set('');
  }

  async load(force = false): Promise<void> {
    if (this.loading() && !force) {
      return;
    }

    this.loading.set(true);
    try {
      const data = await this.api.getAll({
        donViId: this.donViId() || undefined,
      });
      this.items.set(data);
    } finally {
      this.loading.set(false);
    }
  }

  openCreate(): void {
    this.selectedId.set(null);
    this.form.reset({
      nhomCamera: this.activeTab(),
      buongGiamTrangBiSl: 0,
      buongGiamTrangBiTs: 0,
      nhuCauDauTu: 0,
      baoTri: 0,
      suaChua: 0,
      soLanViPham: 0,
      hoSoCapDoAttt: 0,
      cbChuyenTrach: 0,
      cbKiemNhiem: 0,
      cbDiaPhuong: 0,
      daoTaoBo: 0,
      daoTaoNhuCau: 0,
    });
    this.form.markAsPristine();
    this.form.markAsUntouched();
    this.dialogVisible.set(true);
  }

  async select(item: CameraQuanLyDto): Promise<void> {
    const detail = await this.api.getById(item.id);
    this.selectedId.set(detail.id);
    this.form.patchValue({
      nhomCamera: detail.nhomCamera,
      tenDonViDiaChi: detail.tenDonViDiaChi,
      buongGiamTrangBiSl: detail.buongGiamTrangBiSl,
      buongGiamTrangBiTs: detail.buongGiamTrangBiTs,
      nhuCauDauTu: detail.nhuCauDauTu,
      baoTri: detail.baoTri,
      suaChua: detail.suaChua,
      soLanViPham: detail.soLanViPham,
      ketNoiChiaSe: detail.ketNoiChiaSe,
      hoSoCapDoAttt: detail.hoSoCapDoAttt,
      cbChuyenTrach: detail.cbChuyenTrach,
      cbKiemNhiem: detail.cbKiemNhiem,
      cbDiaPhuong: detail.cbDiaPhuong,
      daoTaoBo: detail.daoTaoBo,
      daoTaoNhuCau: detail.daoTaoNhuCau,
      ghiChu: detail.ghiChu,
    });
    this.form.markAsPristine();
    this.form.markAsUntouched();
    this.dialogVisible.set(true);
  }

  resetForm(): void {
    this.selectedId.set(null);
    this.form.reset({
      buongGiamTrangBiSl: 0,
      buongGiamTrangBiTs: 0,
      nhuCauDauTu: 0,
      baoTri: 0,
      suaChua: 0,
      soLanViPham: 0,
      hoSoCapDoAttt: 0,
      cbChuyenTrach: 0,
      cbKiemNhiem: 0,
      cbDiaPhuong: 0,
      daoTaoBo: 0,
      daoTaoNhuCau: 0,
    });
    this.form.markAsPristine();
    this.form.markAsUntouched();
  }

  closeDialog(): void {
    this.dialogVisible.set(false);
  }

  async save(): Promise<void> {
    if (!this.form.valid) {
      this.notificationService.show(
        'warning',
        'Vui lòng điền đầy đủ các trường bắt buộc',
      );
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    try {
      const raw = this.form.getRawValue();
      const formValue: UpsertCameraQuanLyRequest = {
        donViId: this.donViId(),
        nhomCamera: raw.nhomCamera,
        tenDonViDiaChi: raw.tenDonViDiaChi ?? '',
        buongGiamTrangBiSl: raw.buongGiamTrangBiSl ?? 0,
        buongGiamTrangBiTs: raw.buongGiamTrangBiTs ?? 0,
        nhuCauDauTu: raw.nhuCauDauTu ?? 0,
        baoTri: raw.baoTri ?? 0,
        suaChua: raw.suaChua ?? 0,
        soLanViPham: raw.soLanViPham ?? 0,
        ketNoiChiaSe: raw.ketNoiChiaSe,
        hoSoCapDoAttt: raw.hoSoCapDoAttt ?? 0,
        cbChuyenTrach: raw.cbChuyenTrach ?? 0,
        cbKiemNhiem: raw.cbKiemNhiem ?? 0,
        cbDiaPhuong: raw.cbDiaPhuong ?? 0,
        daoTaoBo: raw.daoTaoBo ?? 0,
        daoTaoNhuCau: raw.daoTaoNhuCau ?? 0,
        ghiChu: raw.ghiChu,
      };
      const selectedId = this.selectedId();

      if (selectedId !== null) {
        await this.api.update(selectedId, formValue);
        this.notificationService.show(
          'success',
          'Cập nhật Camera quản lý thành công',
        );
      } else {
        await this.api.create(formValue);
        this.notificationService.show(
          'success',
          'Thêm mới Camera quản lý thành công',
        );
      }

      await this.load(true);
      this.dialogVisible.set(false);
    } catch (error) {
      this.notificationService.show(
        'error',
        `Lỗi: ${error instanceof Error ? error.message : 'Không thể lưu dữ liệu'}`,
      );
    } finally {
      this.saving.set(false);
    }
  }

  async remove(item: CameraQuanLyDto): Promise<void> {
    const confirmed = await this.confirmDialog.confirmDelete({
      message: `Xác nhận xóa bản ghi camera quản lý cho "${item.tenDonViDiaChi}"?`,
      acceptLabel: 'Xóa',
      rejectLabel: 'Hủy',
    });
    if (!confirmed) {
      return;
    }

    this.saving.set(true);
    try {
      await this.api.delete(item.id);
      this.notificationService.show('success', 'Xóa Camera quản lý thành công');
      await this.load(true);
    } catch (error) {
      this.notificationService.show(
        'error',
        `Lỗi: ${error instanceof Error ? error.message : 'Không thể xóa dữ liệu'}`,
      );
    } finally {
      this.saving.set(false);
    }
  }

  formatDisplayDate(dateString: string | null): string {
    if (!dateString) {
      return '—';
    }
    try {
      const date = new Date(dateString);
      return date.toLocaleDateString('vi-VN', {
        day: '2-digit',
        month: '2-digit',
        year: 'numeric',
      });
    } catch {
      return '—';
    }
  }

  hasFieldValue(controlName: CameraFormControlName): boolean {
    const value = this.form.controls[controlName].value;
    if (typeof value === 'number') {
      return true;
    }
    return value !== null && value !== undefined && `${value}`.trim() !== '';
  }

  isFieldInvalid(controlName: CameraFormControlName): boolean {
    const control = this.form.controls[controlName];
    return !!control && control.invalid && control.touched;
  }

  private resolveCameraGroupLabel(
    value: string,
    rawLabel?: string | null,
  ): string {
    const normalized = (rawLabel || '').trim();

    if (value === 'NHOM_1') {
      if (!normalized || /^nh[oó]m(\s*camera)?\s*1$/i.test(normalized)) {
        return this.defaultTabLabels.NHOM_1;
      }
      return normalized;
    }

    if (value === 'NHOM_2') {
      if (!normalized || /^nh[oó]m(\s*camera)?\s*2$/i.test(normalized)) {
        return this.defaultTabLabels.NHOM_2;
      }
      return normalized;
    }

    return normalized || value;
  }

  getTabLabel(tab: CameraTabKey): string {
    return this.tabLabels()[tab];
  }
}
