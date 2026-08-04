import { CommonModule } from '@angular/common';
import { Component, computed, signal } from '@angular/core';
import {
  FormBuilder,
  FormsModule,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { DropdownModule } from 'primeng/dropdown';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { InputTextareaModule } from 'primeng/inputtextarea';
import { TableModule } from 'primeng/table';
import { TooltipModule } from 'primeng/tooltip';
import { AuthService } from '../../core/auth/auth.service';
import { NotificationService } from '../../core/ui/notification.service';
import { HasPermissionDirective } from '../../shared/permission.directive';
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
  CameraThucTrangApi,
  CameraThucTrangDto,
  UpsertCameraThucTrangRequest,
} from './camera-thuc-trang.api';

type CameraTabKey = 'NHOM_1' | 'NHOM_2';

type CameraThucTrangFormControlName =
  | 'donViId'
  | 'kyBaoCaoCode'
  | 'nhomCamera'
  | 'tenHeThong'
  | 'cauHinhIp'
  | 'cauHinhAnalog'
  | 'thucTrangIp'
  | 'thucTrangAnalog'
  | 'chuDauTu'
  | 'namDauTu'
  | 'duongTruyen'
  | 'phanMem'
  | 'luuTru'
  | 'ghiChu';

interface SelectOption<T = string> {
  label: string;
  value: T;
}

@Component({
  selector: 'app-camera-thuc-trang-page',
  standalone: true,
  imports: [
    TongHopModeBannerComponent,
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    SectionCardComponent,
    EmptyStateComponent,
    LoadingOverlayComponent,
    HasPermissionDirective,
    DialogModule,
    DropdownModule,
    InputNumberModule,
    InputTextModule,
    InputTextareaModule,
    ButtonModule,
    TableModule,
    TooltipModule,
  ],
  templateUrl: './camera-thuc-trang.page.html',
  styleUrl: './camera-thuc-trang.page.scss',
})
export class CameraThucTrangPage {
  readonly selectPanelStyleClass = APP_SELECT_PANEL_STYLE_CLASS;
  readonly tableStyleClass = APP_TABLE_STYLE_CLASS;
  readonly tableHeaderCellClass = APP_TABLE_HEADER_CELL_CLASS;
  readonly tableRowClass = APP_TABLE_ROW_CLASS;
  readonly tableBodyCellClass = APP_TABLE_BODY_CELL_CLASS;

  readonly form = this.fb.group({
    donViId: [{ value: 0, disabled: true }, Validators.required],
    kyBaoCaoCode: [{ value: '', disabled: true }, Validators.required],
    nhomCamera: [null as string | null],
    tenHeThong: ['', Validators.required],
    cauHinhIp: [0, [Validators.required, Validators.min(0)]],
    cauHinhAnalog: [0, [Validators.required, Validators.min(0)]],
    thucTrangIp: [0, [Validators.required, Validators.min(0)]],
    thucTrangAnalog: [0, [Validators.required, Validators.min(0)]],
    chuDauTu: [null as string | null],
    namDauTu: [null as number | null],
    duongTruyen: [null as string | null],
    phanMem: [null as string | null],
    luuTru: [null as string | null],
    ghiChu: [null as string | null],
  });

  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly dialogVisible = signal(false);
  readonly items = signal<CameraThucTrangDto[]>([]);
  readonly cameraGroupOptions = signal<SelectOption[]>([]);
  readonly duongTruyenOptions = signal<SelectOption[]>([]);
  readonly selectedId = signal<number | null>(null);
  readonly activeTab = signal<CameraTabKey>('NHOM_1');
  readonly apiError = signal<string | null>(null);
  readonly currentKyBaoCaoCode = signal(this.buildCurrentKyBaoCaoCode());
  readonly filterTenHeThong = signal('');
  readonly canEdit = signal(true);

  readonly donViId = computed(() => this.authService.profile()?.donViId ?? 0);

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
    const keyword = this.filterTenHeThong().trim().toLowerCase();
    const source =
      this.activeTab() === 'NHOM_1' ? this.nhom1Items() : this.nhom2Items();

    if (!keyword) {
      return source;
    }

    return source.filter((item) =>
      String(item.tenHeThong ?? '')
        .toLowerCase()
        .includes(keyword),
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
    private readonly api: CameraThucTrangApi,
    private readonly codesApi: CodesApi,
    private readonly notificationService: NotificationService,
    private readonly confirmDialog: ConfirmDialogWrapperService,
  ) {
    void this.initialize();
  }

  async initialize(): Promise<void> {
    this.loading.set(true);
    try {
      const [cameraCodeDto, duongTruyenCodeDto] = await Promise.all([
        this.codesApi.getByCode('NHOM_CAMERA').catch(() => null),
        this.codesApi.getByCode('DUONG_TRUYEN_CAMERA').catch(() => null),
      ]);

      if (cameraCodeDto?.values) {
        this.cameraGroupOptions.set(
          cameraCodeDto.values.map((code) => ({
            label: this.resolveCameraGroupLabel(code.value, code.name),
            value: code.value,
          })),
        );
      }

      if (duongTruyenCodeDto?.values) {
        this.duongTruyenOptions.set(
          duongTruyenCodeDto.values.map((code) => ({
            label: code.name?.trim() || code.value,
            value: code.value,
          })),
        );
      }

      this.resetForm(this.activeTab());
      await this.load(true);
    } finally {
      this.loading.set(false);
    }
  }

  setActiveTab(tab: CameraTabKey): void {
    if (this.activeTab() === tab) {
      return;
    }

    this.activeTab.set(tab);
    this.filterTenHeThong.set('');

    if (this.dialogVisible() && this.selectedId() === null) {
      this.form.patchValue({ nhomCamera: tab });
    }
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
    this.apiError.set(null);
    this.selectedId.set(null);
    this.resetForm(this.activeTab());
    this.dialogVisible.set(true);
  }

  async select(item: CameraThucTrangDto): Promise<void> {
    this.apiError.set(null);
    const detail = await this.api.getById(item.id);
    const nhom = detail.nhomCamera === 'NHOM_2' ? 'NHOM_2' : 'NHOM_1';

    this.activeTab.set(nhom);
    this.selectedId.set(detail.id);
    this.form.reset({
      donViId: detail.donViId,
      kyBaoCaoCode: detail.kyBaoCaoCode || this.currentKyBaoCaoCode(),
      nhomCamera: detail.nhomCamera,
      tenHeThong: detail.tenHeThong,
      cauHinhIp: detail.cauHinhIp,
      cauHinhAnalog: detail.cauHinhAnalog,
      thucTrangIp: detail.thucTrangIp,
      thucTrangAnalog: detail.thucTrangAnalog,
      chuDauTu: detail.chuDauTu,
      namDauTu: detail.namDauTu,
      duongTruyen: detail.duongTruyen,
      phanMem: detail.phanMem,
      luuTru: detail.luuTru,
      ghiChu: detail.ghiChu,
    });
    this.form.markAsPristine();
    this.form.markAsUntouched();
    this.dialogVisible.set(true);
  }

  closeDialog(): void {
    this.dialogVisible.set(false);
    this.apiError.set(null);
    this.resetForm(this.activeTab());
  }

  async save(): Promise<void> {
    if (!this.form.valid) {
      this.form.markAllAsTouched();
      this.notificationService.show(
        'warning',
        'Vui lòng điền đầy đủ các trường bắt buộc',
      );
      return;
    }

    this.saving.set(true);
    this.apiError.set(null);

    try {
      const raw = this.form.getRawValue();
      const payload: UpsertCameraThucTrangRequest = {
        donViId: this.donViId(),
        nhomCamera: raw.nhomCamera || this.activeTab(),
        tenHeThong: raw.tenHeThong?.trim() || '',
        cauHinhIp: raw.cauHinhIp ?? 0,
        cauHinhAnalog: raw.cauHinhAnalog ?? 0,
        thucTrangIp: raw.thucTrangIp ?? 0,
        thucTrangAnalog: raw.thucTrangAnalog ?? 0,
        chuDauTu: raw.chuDauTu?.trim() || null,
        namDauTu: raw.namDauTu ?? null,
        duongTruyen: raw.duongTruyen,
        phanMem: raw.phanMem?.trim() || null,
        luuTru: raw.luuTru?.trim() || null,
        ghiChu: raw.ghiChu?.trim() || null,
      };

      if (this.selectedId() !== null) {
        await this.api.update(this.selectedId()!, payload);
        this.notificationService.show(
          'success',
          'Cập nhật Camera thực trạng thành công',
        );
      } else {
        await this.api.create(payload);
        this.notificationService.show(
          'success',
          'Thêm mới Camera thực trạng thành công',
        );
      }

      await this.load(true);
      this.closeDialog();
    } catch (error) {
      const message =
        error instanceof Error ? error.message : 'Không thể lưu dữ liệu';
      this.apiError.set(message);
      this.notificationService.show('error', `Lỗi: ${message}`);
    } finally {
      this.saving.set(false);
    }
  }

  async remove(item: CameraThucTrangDto): Promise<void> {
    const confirmed = await this.confirmDialog.confirmDelete({
      message: `Xác nhận xóa bản ghi camera thực trạng cho "${item.tenHeThong}"?`,
      acceptLabel: 'Xóa',
      rejectLabel: 'Hủy',
    });

    if (!confirmed) {
      return;
    }

    this.saving.set(true);
    try {
      await this.api.delete(item.id);
      this.notificationService.show(
        'success',
        'Xóa Camera thực trạng thành công',
      );
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

  hasFieldValue(controlName: CameraThucTrangFormControlName): boolean {
    const value = this.form.controls[controlName].value;

    if (typeof value === 'number') {
      return true;
    }

    return value !== null && value !== undefined && `${value}`.trim() !== '';
  }

  isFieldInvalid(controlName: CameraThucTrangFormControlName): boolean {
    const control = this.form.controls[controlName];
    return !!control && control.invalid && control.touched;
  }

  getTabLabel(tab: CameraTabKey): string {
    return this.tabLabels()[tab];
  }

  getDuongTruyenLabel(value: string | null): string {
    if (!value) {
      return '—';
    }

    return (
      this.duongTruyenOptions().find((option) => option.value === value)
        ?.label || value
    );
  }

  private resetForm(tab: CameraTabKey): void {
    this.form.reset({
      donViId: this.donViId(),
      kyBaoCaoCode: this.currentKyBaoCaoCode(),
      nhomCamera: tab,
      tenHeThong: '',
      cauHinhIp: 0,
      cauHinhAnalog: 0,
      thucTrangIp: 0,
      thucTrangAnalog: 0,
      chuDauTu: null,
      namDauTu: null,
      duongTruyen: null,
      phanMem: null,
      luuTru: null,
      ghiChu: null,
    });
    this.form.markAsPristine();
    this.form.markAsUntouched();
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

  private buildCurrentKyBaoCaoCode(): string {
    const now = new Date();
    const quarter = Math.floor(now.getMonth() / 3) + 1;
    return `${now.getFullYear()}Q${quarter}`;
  }
}
