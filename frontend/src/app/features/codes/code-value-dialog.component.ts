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
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { DropdownModule } from 'primeng/dropdown';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { InputTextareaModule } from 'primeng/inputtextarea';

export type CodeValueDialogMode = 'create' | 'edit';

export interface CodeValueInitialData {
  id?: number;
  value: string;
  name: string;
  description?: string | null;
  sortOrder: number;
  isActive: boolean;
}

export interface CodeValueSubmitPayload {
  mode: CodeValueDialogMode;
  id?: number;
  value: string;
  name: string;
  description?: string | null;
  sortOrder: number;
  isActive: boolean;
}

@Component({
  selector: 'app-code-value-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    DialogModule,
    DropdownModule,
    InputTextModule,
    InputNumberModule,
    InputTextareaModule,
    ButtonModule,
  ],
  templateUrl: './code-value-dialog.component.html',
  styleUrl: './code-value-dialog.component.scss',
})
export class CodeValueDialogComponent implements OnChanges {
  @Input() visible = false;
  @Input() submitting = false;
  @Input() mode: CodeValueDialogMode = 'create';
  @Input() initialData: CodeValueInitialData | null = null;

  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() save = new EventEmitter<CodeValueSubmitPayload>();

  readonly statusOptions = [
    { label: 'Đang hoạt động', value: true },
    { label: 'Ngừng hoạt động', value: false },
  ];

  readonly form = this.formBuilder.group({
    value: ['', [Validators.required, Validators.maxLength(100)]],
    name: ['', [Validators.required, Validators.maxLength(200)]],
    sortOrder: [0, [Validators.required]],
    isActive: [true, [Validators.required]],
    description: ['', [Validators.maxLength(500)]],
  });

  constructor(private readonly formBuilder: FormBuilder) {}

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['visible'] && this.visible) {
      this.form.reset({
        value: this.initialData?.value ?? '',
        name: this.initialData?.name ?? '',
        sortOrder: this.initialData?.sortOrder ?? 0,
        isActive: this.initialData?.isActive ?? true,
        description: this.initialData?.description ?? '',
      });
    }
  }

  get dialogTitle(): string {
    return this.mode === 'create'
      ? 'Thêm giá trị danh mục'
      : 'Cập nhật giá trị danh mục';
  }

  closeDialog(): void {
    this.visibleChange.emit(false);
  }

  onDialogHide(): void {
    this.closeDialog();
  }

  isInvalid(controlName: string): boolean {
    const control = this.form.get(controlName);
    return !!control && control.invalid && (control.touched || control.dirty);
  }

  getError(controlName: string): string {
    const control = this.form.get(controlName);
    if (!control?.errors) {
      return '';
    }

    if (control.errors['required']) {
      switch (controlName) {
        case 'value':
          return 'Vui lòng nhập giá trị';
        case 'name':
          return 'Vui lòng nhập tên hiển thị';
        default:
          return 'Trường này là bắt buộc';
      }
    }

    if (control.errors['maxlength']) {
      return 'Dữ liệu vượt quá độ dài cho phép';
    }

    return 'Dữ liệu không hợp lệ';
  }

  submit(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      return;
    }

    const raw = this.form.getRawValue();
    this.save.emit({
      mode: this.mode,
      id: this.initialData?.id,
      value: (raw.value ?? '').trim(),
      name: (raw.name ?? '').trim(),
      description: (raw.description ?? '').trim() || null,
      sortOrder: Number(raw.sortOrder ?? 0),
      isActive: !!raw.isActive,
    });
  }
}
