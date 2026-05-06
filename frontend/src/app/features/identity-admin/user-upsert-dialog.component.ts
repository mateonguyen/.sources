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
import { PasswordModule } from 'primeng/password';
import {
  APP_DIALOG_CONTENT_STYLE_CLASS,
  APP_DIALOG_MASK_STYLE_CLASS,
  APP_DIALOG_STYLE_CLASS,
  APP_SELECT_PANEL_STYLE_CLASS,
} from '../../shared/ui/primeng-pt';

export type UserDialogMode = 'create' | 'edit';

export interface UserUpsertInitialData {
  id?: number;
  username: string;
  hoTen: string;
  donViId: number | null;
  isActive: boolean;
  email?: string | null;
  soDienThoai?: string | null;
  roleId?: number | null;
  mustChangePassword?: boolean;
}

export interface UserUpsertSubmitPayload {
  mode: UserDialogMode;
  id?: number;
  username: string;
  hoTen: string;
  donViId: number;
  isActive: boolean;
  email?: string | null;
  soDienThoai?: string | null;
  roleId?: number | null;
  password?: string;
  mustChangePassword: boolean;
}

interface SelectOption {
  label: string;
  value: number;
}

@Component({
  selector: 'app-user-upsert-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    DialogModule,
    DropdownModule,
    InputTextModule,
    PasswordModule,
    ButtonModule,
  ],
  templateUrl: './user-upsert-dialog.component.html',
  styleUrl: './user-upsert-dialog.component.scss',
})
export class UserUpsertDialogComponent implements OnChanges {
  readonly dialogStyleClass = APP_DIALOG_STYLE_CLASS;
  readonly dialogContentStyleClass = APP_DIALOG_CONTENT_STYLE_CLASS;
  readonly dialogMaskStyleClass = APP_DIALOG_MASK_STYLE_CLASS;
  readonly selectPanelStyleClass = APP_SELECT_PANEL_STYLE_CLASS;


  @Input() visible = false;
  @Input() mode: UserDialogMode = 'create';
  @Input() submitting = false;
  @Input() donViOptions: SelectOption[] = [];
  @Input() roleOptions: SelectOption[] = [];
  @Input() initialData: UserUpsertInitialData | null = null;

  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() save = new EventEmitter<UserUpsertSubmitPayload>();

  readonly statusOptions = [
    { label: 'Đang hoạt động', value: true },
    { label: 'Ngừng hoạt động', value: false },
  ];

  readonly form = this.fb.group(
    {
      username: ['', [Validators.required, Validators.maxLength(100)]],
      hoTen: ['', [Validators.required, Validators.maxLength(150)]],
      donViId: [null as number | null, [Validators.required]],
      isActive: [true, [Validators.required]],
      email: ['', [Validators.email, Validators.maxLength(255)]],
      soDienThoai: ['', [Validators.pattern(/^[0-9+()\-\s]{8,15}$/)]],
      roleId: [null as number | null],
      password: [''],
      confirmPassword: [''],
    },
    {
      validators: [this.passwordsMatchValidator()],
    },
  );

  constructor(private readonly fb: FormBuilder) {}

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['visible'] && this.visible) {
      this.setupFormByMode();
    }
  }

  get dialogTitle(): string {
    return this.mode === 'create' ? 'Thêm người dùng' : 'Cập nhật người dùng';
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

  onSubmit(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      return;
    }

    const raw = this.form.getRawValue();
    const payload: UserUpsertSubmitPayload = {
      mode: this.mode,
      id: this.initialData?.id,
      username: (raw.username ?? '').trim(),
      hoTen: (raw.hoTen ?? '').trim(),
      donViId: raw.donViId as number,
      isActive: !!raw.isActive,
      email: this.normalizeOptional(raw.email),
      soDienThoai: this.normalizeOptional(raw.soDienThoai),
      roleId: raw.roleId,
      mustChangePassword: this.initialData?.mustChangePassword ?? false,
    };

    if (this.isCreateMode) {
      payload.password = raw.password?.trim() ?? '';
    }

    this.save.emit(payload);
  }

  isInvalid(controlName: string): boolean {
    const control = this.form.get(controlName);
    return !!control && control.invalid && control.touched;
  }

  getError(controlName: string): string {
    const control = this.form.get(controlName);
    if (!control?.errors) {
      return '';
    }

    if (control.errors['required']) {
      switch (controlName) {
        case 'username':
          return 'Vui lòng nhập tên đăng nhập';
        case 'hoTen':
          return 'Vui lòng nhập họ và tên';
        case 'donViId':
          return 'Vui lòng chọn đơn vị';
        case 'password':
          return 'Vui lòng nhập mật khẩu';
        case 'confirmPassword':
          return 'Vui lòng xác nhận mật khẩu';
        default:
          return 'Trường này là bắt buộc';
      }
    }

    if (control.errors['email']) {
      return 'Email không đúng định dạng';
    }

    if (control.errors['pattern']) {
      return 'Số điện thoại không đúng định dạng';
    }

    if (control.errors['minlength']) {
      return 'Mật khẩu phải có ít nhất 8 ký tự';
    }

    if (control.errors['maxlength']) {
      return 'Dữ liệu vượt quá độ dài cho phép';
    }

    return 'Dữ liệu không hợp lệ';
  }

  get passwordMismatchError(): string {
    return this.form.errors?.['passwordMismatch'] &&
      this.form.get('confirmPassword')?.touched
      ? 'Mật khẩu xác nhận không khớp'
      : '';
  }

  private setupFormByMode(): void {
    const seed = this.initialData;

    this.form.reset({
      username: seed?.username ?? '',
      hoTen: seed?.hoTen ?? '',
      donViId: seed?.donViId ?? null,
      isActive: seed?.isActive ?? true,
      email: seed?.email ?? '',
      soDienThoai: seed?.soDienThoai ?? '',
      roleId: seed?.roleId ?? null,
      password: '',
      confirmPassword: '',
    });

    const usernameControl = this.form.get('username');
    const passwordControl = this.form.get('password');
    const confirmControl = this.form.get('confirmPassword');

    if (this.isCreateMode) {
      usernameControl?.enable({ emitEvent: false });
      passwordControl?.setValidators([
        Validators.required,
        Validators.minLength(8),
        Validators.maxLength(100),
      ]);
      confirmControl?.setValidators([
        Validators.required,
        Validators.maxLength(100),
      ]);
    } else {
      usernameControl?.disable({ emitEvent: false });
      passwordControl?.clearValidators();
      confirmControl?.clearValidators();
    }

    passwordControl?.updateValueAndValidity({ emitEvent: false });
    confirmControl?.updateValueAndValidity({ emitEvent: false });
    this.form.updateValueAndValidity({ emitEvent: false });
  }

  private passwordsMatchValidator(): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      const group = control as typeof this.form;
      const password = group.get('password')?.value ?? '';
      const confirm = group.get('confirmPassword')?.value ?? '';
      if (!this.isCreateMode) {
        return null;
      }
      return password && confirm && password !== confirm
        ? { passwordMismatch: true }
        : null;
    };
  }

  private normalizeOptional(value: string | null | undefined): string | null {
    const normalized = (value ?? '').trim();
    return normalized ? normalized : null;
  }
}
