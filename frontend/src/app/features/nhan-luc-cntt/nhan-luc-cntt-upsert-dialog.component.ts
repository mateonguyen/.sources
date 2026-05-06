import { CommonModule } from '@angular/common';
import {
  Component,
  EventEmitter,
  Input,
  OnChanges,
  Output,
  SimpleChanges,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { PrimeNGConfig } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CalendarModule } from 'primeng/calendar';
import { DialogModule } from 'primeng/dialog';
import { DropdownModule } from 'primeng/dropdown';
import { InputTextModule } from 'primeng/inputtext';
import { InputTextareaModule } from 'primeng/inputtextarea';
import {
  APP_DIALOG_CONTENT_STYLE_CLASS,
  APP_DIALOG_MASK_STYLE_CLASS,
  APP_DIALOG_STYLE_CLASS,
  APP_SELECT_PANEL_STYLE_CLASS,
} from '../../shared/ui/primeng-pt';
import {
  NhanLucCnttUpsertInitialData,
  NhanLucCnttUpsertSubmitEvent,
  SelectOption,
} from './nhan-luc-cntt.models';

export type NhanLucCnttDialogMode = 'create' | 'edit';

@Component({
  selector: 'app-nhan-luc-cntt-upsert-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    DialogModule,
    CalendarModule,
    DropdownModule,
    InputTextModule,
    InputTextareaModule,
    ButtonModule,
  ],
  templateUrl: './nhan-luc-cntt-upsert-dialog.component.html',
  styleUrl: './nhan-luc-cntt-upsert-dialog.component.scss',
})
export class NhanLucCnttUpsertDialogComponent implements OnChanges {
  readonly dialogStyleClass = APP_DIALOG_STYLE_CLASS;
  readonly dialogContentStyleClass = APP_DIALOG_CONTENT_STYLE_CLASS;
  readonly dialogMaskStyleClass = APP_DIALOG_MASK_STYLE_CLASS;
  readonly selectPanelStyleClass = APP_SELECT_PANEL_STYLE_CLASS;
  readonly dropdownPanelStyleClass = `${APP_SELECT_PANEL_STYLE_CLASS} nhan-luc-dialog__dropdown-panel`;
  readonly calendarPanelStyleClass = `${APP_SELECT_PANEL_STYLE_CLASS} nhan-luc-dialog__calendar-panel`;
  readonly calendarLocaleVi = {
    firstDayOfWeek: 1,
    dayNames: [
      'Chủ nhật',
      'Thứ Hai',
      'Thứ Ba',
      'Thứ Tư',
      'Thứ Năm',
      'Thứ Sáu',
      'Thứ Bảy',
    ],
    dayNamesShort: ['CN', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7'],
    dayNamesMin: ['CN', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7'],
    monthNames: [
      'Tháng 1',
      'Tháng 2',
      'Tháng 3',
      'Tháng 4',
      'Tháng 5',
      'Tháng 6',
      'Tháng 7',
      'Tháng 8',
      'Tháng 9',
      'Tháng 10',
      'Tháng 11',
      'Tháng 12',
    ],
    monthNamesShort: [
      'T1',
      'T2',
      'T3',
      'T4',
      'T5',
      'T6',
      'T7',
      'T8',
      'T9',
      'T10',
      'T11',
      'T12',
    ],
    today: 'Hôm nay',
    clear: 'Xóa',
    weekHeader: 'Tuần',
  };

  @Input() visible = false;
  @Input() mode: NhanLucCnttDialogMode = 'create';
  @Input() submitting = false;
  @Input() defaultDonViId: number | null = null;
  @Input() donViOptions: SelectOption<number>[] = [];
  @Input() donViCongTacOptions: SelectOption<number | null>[] = [];
  @Input() gioiTinhOptions: SelectOption<string | null>[] = [];
  @Input() capBacOptions: SelectOption<string | null>[] = [];
  @Input() loaiNhanLucOptions: SelectOption<string | null>[] = [];
  @Input() trinhDoCnttOptions: SelectOption<string | null>[] = [];
  @Input() trinhDoLlctOptions: SelectOption<string | null>[] = [];
  @Input() initialData: NhanLucCnttUpsertInitialData | null = null;

  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() save = new EventEmitter<NhanLucCnttUpsertSubmitEvent>();

  readonly form = this.fb.group({
    donViId: [null as number | null, [Validators.required]],
    donViCongTacId: [null as number | null],
    hoTen: ['', [Validators.required, Validators.maxLength(200)]],
    ngaySinh: [null as Date | null],
    gioiTinh: [null as string | null],
    capBac: [null as string | null],
    chucVu: ['', [Validators.maxLength(200)]],
    dienThoai: ['', [Validators.pattern(/^[0-9+()\-\s]{8,20}$/)]],
    loaiNhanLuc: [null as string | null],
    trinhDoCntt: [null as string | null],
    trinhDoLlct: [null as string | null],
    ghiChu: ['', [Validators.maxLength(2000)]],
  });

  constructor(
    private readonly fb: FormBuilder,
    private readonly primeNgConfig: PrimeNGConfig,
  ) {
    this.primeNgConfig.setTranslation(this.calendarLocaleVi);
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['visible'] && this.visible) {
      this.resetForm();
    }
  }

  get dialogTitle(): string {
    return this.mode === 'create'
      ? 'Thêm nhân lực CNTT'
      : 'Chỉnh sửa nhân lực CNTT';
  }

  get isCreateMode(): boolean {
    return this.mode === 'create';
  }

  closeDialog(): void {
    this.visibleChange.emit(false);
  }

  onDialogHide(): void {
    this.closeDialog();
  }

  submit(keepDialogOpen: boolean): void {
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      return;
    }

    const raw = this.form.getRawValue();
    this.save.emit({
      id: this.initialData?.id,
      keepDialogOpen,
      payload: {
        donViId: raw.donViId as number,
        donViCongTacId: raw.donViCongTacId,
        hoTen: (raw.hoTen ?? '').trim(),
        ngaySinh: this.normalizeNgaySinh(raw.ngaySinh),
        gioiTinh: raw.gioiTinh,
        capBac: raw.capBac,
        chucVu: this.normalizeOptional(raw.chucVu),
        dienThoai: this.normalizeOptional(raw.dienThoai),
        loaiNhanLuc: raw.loaiNhanLuc,
        trinhDoCntt: raw.trinhDoCntt,
        trinhDoLlct: raw.trinhDoLlct,
        ghiChu: this.normalizeOptional(raw.ghiChu),
      },
    });
  }

  isInvalid(controlName: string): boolean {
    const control = this.form.get(controlName);
    return !!control && control.invalid && (control.dirty || control.touched);
  }

  getError(controlName: string): string {
    const control = this.form.get(controlName);
    if (!control?.errors) {
      return '';
    }

    if (control.errors['required']) {
      switch (controlName) {
        case 'donViId':
          return 'Vui lòng chọn đơn vị báo cáo';
        case 'hoTen':
          return 'Vui lòng nhập họ và tên';
        default:
          return 'Trường này là bắt buộc';
      }
    }

    if (control.errors['maxlength']) {
      return 'Dữ liệu vượt quá độ dài cho phép';
    }

    if (control.errors['pattern']) {
      return 'Số điện thoại không đúng định dạng';
    }

    return 'Dữ liệu không hợp lệ';
  }

  private resetForm(): void {
    const seed = this.initialData;

    this.form.reset({
      donViId: seed?.donViId ?? this.defaultDonViId,
      donViCongTacId: seed?.donViCongTacId ?? null,
      hoTen: seed?.hoTen ?? '',
      ngaySinh: this.parseNgaySinh(seed?.ngaySinh),
      gioiTinh: seed?.gioiTinh ?? null,
      capBac: seed?.capBac ?? null,
      chucVu: seed?.chucVu ?? '',
      dienThoai: seed?.dienThoai ?? '',
      loaiNhanLuc: seed?.loaiNhanLuc ?? null,
      trinhDoCntt: seed?.trinhDoCntt ?? null,
      trinhDoLlct: seed?.trinhDoLlct ?? null,
      ghiChu: seed?.ghiChu ?? '',
    });
  }

  private normalizeOptional(value: string | null | undefined): string | null {
    const normalized = value?.trim();
    return normalized ? normalized : null;
  }

  private normalizeNgaySinh(
    value: Date | string | null | undefined,
  ): string | null {
    if (!value) {
      return null;
    }

    if (typeof value === 'string') {
      const normalized = value.trim();
      if (!normalized) {
        return null;
      }

      const ddmmyyyy = /^(\d{2})\/(\d{2})\/(\d{4})$/;
      const ddmmyyyyMatch = normalized.match(ddmmyyyy);
      if (ddmmyyyyMatch) {
        const [, dd, mm, yyyy] = ddmmyyyyMatch;
        return `${yyyy}-${mm}-${dd}`;
      }

      return normalized;
    }

    return this.toIsoDateString(value);
  }

  private parseNgaySinh(value: string | null | undefined): Date | null {
    if (!value) {
      return null;
    }

    const normalized = value.trim();
    if (!normalized) {
      return null;
    }

    const iso = /^(\d{4})-(\d{2})-(\d{2})$/;
    const isoMatch = normalized.match(iso);
    if (isoMatch) {
      const [, yyyy, mm, dd] = isoMatch;
      return new Date(Number(yyyy), Number(mm) - 1, Number(dd));
    }

    const parsed = new Date(normalized);
    return Number.isNaN(parsed.getTime()) ? null : parsed;
  }

  private toIsoDateString(date: Date): string {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }
}
