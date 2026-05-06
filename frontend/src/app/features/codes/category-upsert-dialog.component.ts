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

export type CategoryUpsertDialogMode = 'create' | 'edit';

export interface CategoryUpsertInitialData {
  id?: number;
  code: string;
  name: string;
  description?: string | null;
  sortOrder: number;
  isActive: boolean;
}

export interface CategoryUpsertSubmitPayload {
  mode: CategoryUpsertDialogMode;
  id?: number;
  code: string;
  name: string;
  description?: string | null;
  sortOrder: number;
  isActive: boolean;
}

@Component({
  selector: 'app-category-upsert-dialog',
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
  templateUrl: './category-upsert-dialog.component.html',
  styleUrl: './category-upsert-dialog.component.scss',
})
export class CategoryUpsertDialogComponent implements OnChanges {
  @Input() visible = false;
  @Input() submitting = false;
  @Input() mode: CategoryUpsertDialogMode = 'create';
  @Input() initialData: CategoryUpsertInitialData | null = null;

  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() save = new EventEmitter<CategoryUpsertSubmitPayload>();

  readonly statusOptions = [
    { label: 'Hoạt động', value: true },
    { label: 'Tạm ngưng', value: false },
  ];

  readonly form = this.formBuilder.group({
    code: ['', [Validators.required, Validators.maxLength(100)]],
    name: ['', [Validators.required, Validators.maxLength(200)]],
    isActive: [true, [Validators.required]],
    sortOrder: [0, [Validators.required]],
    description: ['', [Validators.maxLength(500)]],
  });

  constructor(private readonly formBuilder: FormBuilder) {}

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['visible'] && this.visible) {
      this.form.reset({
        code: this.initialData?.code ?? '',
        name: this.initialData?.name ?? '',
        isActive: this.initialData?.isActive ?? true,
        sortOrder: this.initialData?.sortOrder ?? 0,
        description: this.initialData?.description ?? '',
      });
    }
  }

  get dialogTitle(): string {
    return this.mode === 'create' ? 'Thêm danh mục' : 'Cập nhật danh mục';
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
        case 'code':
          return 'Vui lòng nhập mã danh mục';
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
      code: (raw.code ?? '').trim(),
      name: (raw.name ?? '').trim(),
      description: (raw.description ?? '').trim() || null,
      sortOrder: Number(raw.sortOrder ?? 0),
      isActive: !!raw.isActive,
    });
  }
}
