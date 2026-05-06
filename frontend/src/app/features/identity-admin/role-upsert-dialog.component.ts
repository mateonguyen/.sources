import { CommonModule } from '@angular/common';
import {
  Component,
  EventEmitter,
  Input,
  OnChanges,
  Output,
  SimpleChanges,
} from '@angular/core';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  ValidatorFn,
  Validators,
} from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { DropdownModule } from 'primeng/dropdown';
import { InputTextModule } from 'primeng/inputtext';
import { InputTextareaModule } from 'primeng/inputtextarea';
import {
  APP_DIALOG_CONTENT_STYLE_CLASS,
  APP_DIALOG_MASK_STYLE_CLASS,
  APP_DIALOG_STYLE_CLASS,
} from '../../shared/ui/primeng-pt';

export type RoleDialogMode = 'create' | 'edit';

export interface RoleUpsertInitialData {
  id?: number;
  roleCode: string;
  tenRole: string;
  moTa?: string | null;
}

export interface RoleUpsertSubmitPayload {
  mode: RoleDialogMode;
  id?: number;
  roleCode: string;
  tenRole: string;
  moTa?: string | null;
}

@Component({
  selector: 'app-role-upsert-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    DialogModule,
    InputTextModule,
    InputTextareaModule,
    DropdownModule,
    ButtonModule,
  ],
  templateUrl: './role-upsert-dialog.component.html',
  styleUrl: './role-upsert-dialog.component.scss',
})
export class RoleUpsertDialogComponent implements OnChanges {
  readonly dialogStyleClass = APP_DIALOG_STYLE_CLASS;
  readonly dialogContentStyleClass = APP_DIALOG_CONTENT_STYLE_CLASS;
  readonly dialogMaskStyleClass = APP_DIALOG_MASK_STYLE_CLASS;

  @Input() visible = false;
  @Input() mode: RoleDialogMode = 'create';
  @Input() submitting = false;
  @Input() initialData: RoleUpsertInitialData | null = null;

  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() save = new EventEmitter<RoleUpsertSubmitPayload>();

  readonly form = this.fb.group({
    roleCode: [
      '',
      [
        Validators.required,
        Validators.maxLength(100),
        Validators.pattern(/^[A-Za-z0-9_]+$/),
      ],
    ],
    tenRole: ['', [Validators.required, Validators.maxLength(150)]],
    moTa: ['', [Validators.maxLength(500)]],
  });

  constructor(private readonly fb: FormBuilder) {}

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['visible'] && this.visible) {
      this.seedForm();
    }
  }

  get dialogTitle(): string {
    return this.mode === 'create' ? 'Thêm vai trò' : 'Sửa vai trò';
  }

  closeDialog(): void {
    this.visibleChange.emit(false);
  }

  onDialogHide(): void {
    this.closeDialog();
  }

  onSubmit(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      return;
    }

    const raw = this.form.getRawValue();
    this.save.emit({
      mode: this.mode,
      id: this.initialData?.id,
      roleCode: (raw.roleCode ?? '').trim().toUpperCase(),
      tenRole: (raw.tenRole ?? '').trim(),
      moTa: this.normalizeOptional(raw.moTa),
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
      if (controlName === 'roleCode') {
        return 'Vui lòng nhập mã vai trò';
      }
      if (controlName === 'tenRole') {
        return 'Vui lòng nhập tên vai trò';
      }
      return 'Trường này là bắt buộc';
    }

    if (control.errors['pattern']) {
      return 'Mã vai trò chỉ gồm chữ, số và dấu gạch dưới';
    }

    if (control.errors['maxlength']) {
      return 'Dữ liệu vượt quá độ dài cho phép';
    }

    return 'Dữ liệu không hợp lệ';
  }

  private seedForm(): void {
    const seed = this.initialData;
    this.form.reset({
      roleCode: seed?.roleCode ?? '',
      tenRole: seed?.tenRole ?? '',
      moTa: seed?.moTa ?? '',
    });
  }

  private normalizeOptional(value: string | null | undefined): string | null {
    const normalized = (value ?? '').trim();
    return normalized ? normalized : null;
  }
}
