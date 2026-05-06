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
  FormBuilder,
  FormControl,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { startWith } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { DialogModule } from 'primeng/dialog';
import { DropdownModule } from 'primeng/dropdown';
import { InputTextModule } from 'primeng/inputtext';
import { InputTextareaModule } from 'primeng/inputtextarea';
import { MauBaoCaoModuleCatalogGroupDto, TanSuat } from './mau-bao-cao.api';
import {
  APP_DIALOG_MASK_STYLE_CLASS,
  APP_SELECT_PANEL_STYLE_CLASS,
} from '../../shared/ui/primeng-pt';

export type MauBaoCaoDialogMode = 'create' | 'edit';

export interface MauBaoCaoDialogInitialData {
  id?: number;
  maMau: string;
  tenMau: string;
  tanSuat: TanSuat;
  moduleCodes: string[];
  moTa?: string | null;
  isActive: boolean;
}

export interface MauBaoCaoDialogSubmitPayload {
  mode: MauBaoCaoDialogMode;
  id?: number;
  maMau: string;
  tenMau: string;
  tanSuat: TanSuat;
  moduleCodes: string[];
  moTa?: string | null;
  isActive: boolean;
}

interface ModuleGroupOption {
  code: string;
  label: string;
}

interface ModuleGroupViewModel {
  groupKey: string;
  groupLabel: string;
  items: ModuleGroupOption[];
  totalCount: number;
  selectedCount: number;
  allSelected: boolean;
  partiallySelected: boolean;
}

@Component({
  selector: 'app-mau-bao-cao-upsert-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    DialogModule,
    DropdownModule,
    CheckboxModule,
    InputTextModule,
    InputTextareaModule,
    ButtonModule,
  ],
  templateUrl: './mau-bao-cao-upsert-dialog.component.html',
  styleUrl: './mau-bao-cao-upsert-dialog.component.scss',
})
export class MauBaoCaoUpsertDialogComponent implements OnChanges {
  readonly dialogMaskStyleClass = APP_DIALOG_MASK_STYLE_CLASS;
  readonly selectPanelStyleClass = APP_SELECT_PANEL_STYLE_CLASS;

  @Input() visible = false;
  @Input() submitting = false;
  @Input() mode: MauBaoCaoDialogMode = 'create';
  @Input() initialData: MauBaoCaoDialogInitialData | null = null;
  @Input() moduleCatalogGroups: MauBaoCaoModuleCatalogGroupDto[] = [];

  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() save = new EventEmitter<MauBaoCaoDialogSubmitPayload>();

  readonly tanSuatOptions: Array<{ label: string; value: TanSuat }> = [
    { label: 'Tháng', value: 1 },
    { label: 'Quý', value: 2 },
    { label: 'Năm', value: 3 },
  ];

  readonly moduleSearchControl = new FormControl('', { nonNullable: true });

  filteredModuleGroups: ModuleGroupViewModel[] = [];
  totalSelectedModuleCount = 0;
  private expandedGroupKeys = new Set<string>();

  readonly form = this.formBuilder.group({
    maMau: ['', [Validators.required, Validators.maxLength(50)]],
    tenMau: ['', [Validators.required, Validators.maxLength(200)]],
    tanSuat: [2 as TanSuat, [Validators.required]],
    moduleCodes: [[] as string[], [Validators.required]],
    moTa: ['', [Validators.maxLength(1000)]],
    isActive: [true, [Validators.required]],
  });

  constructor(private readonly formBuilder: FormBuilder) {
    this.moduleSearchControl.valueChanges
      .pipe(startWith(this.moduleSearchControl.value))
      .subscribe(() => this.rebuildModuleViewModel());

    this.form.controls.moduleCodes.valueChanges
      .pipe(startWith(this.form.controls.moduleCodes.value))
      .subscribe((value) => {
        this.totalSelectedModuleCount = value?.length ?? 0;
        this.rebuildModuleViewModel();
      });
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['moduleCatalogGroups']) {
      this.rebuildModuleViewModel();
    }

    if (changes['visible'] && this.visible) {
      this.expandedGroupKeys.clear();
      this.moduleSearchControl.setValue('');

      this.form.reset({
        maMau: this.initialData?.maMau ?? '',
        tenMau: this.initialData?.tenMau ?? '',
        tanSuat: this.initialData?.tanSuat ?? 2,
        moduleCodes: this.initialData?.moduleCodes ?? [],
        moTa: this.initialData?.moTa ?? '',
        isActive: this.initialData?.isActive ?? true,
      });

      this.totalSelectedModuleCount =
        this.form.controls.moduleCodes.value?.length ?? 0;
      this.rebuildModuleViewModel();
    }
  }

  get dialogTitle(): string {
    return this.mode === 'create' ? 'Thêm mẫu báo cáo' : 'Cập nhật mẫu báo cáo';
  }

  get submitLabel(): string {
    return this.mode === 'create' ? 'Lưu mẫu' : 'Cập nhật';
  }

  get hasModuleSearchResult(): boolean {
    return this.filteredModuleGroups.length > 0;
  }

  get moduleSelectionSummary(): string {
    const totalModuleCount = this.moduleCatalogGroups.reduce(
      (sum, group) => sum + group.items.filter((item) => item.isActive).length,
      0,
    );

    return `${totalModuleCount} module`;
  }

  isGroupExpanded(groupKey: string): boolean {
    if (this.moduleSearchControl.value.trim()) {
      return true;
    }

    return this.expandedGroupKeys.has(groupKey);
  }

  toggleGroupExpanded(groupKey: string): void {
    if (this.expandedGroupKeys.has(groupKey)) {
      this.expandedGroupKeys.delete(groupKey);
      return;
    }

    this.expandedGroupKeys.clear();
    this.expandedGroupKeys.add(groupKey);
  }

  private buildFilteredModuleGroups(): ModuleGroupViewModel[] {
    const keyword = this.normalizeKeyword(this.moduleSearchControl.value);
    const selectedCodes = new Set(this.form.controls.moduleCodes.value ?? []);

    return this.moduleCatalogGroups
      .map((group) => {
        const activeItems = group.items
          .filter((item) => item.isActive)
          .map((item) => ({
            code: item.code,
            label: item.label,
          }));

        const visibleItems = keyword
          ? activeItems.filter(
              (item) =>
                this.normalizeKeyword(item.label).includes(keyword) ||
                this.normalizeKeyword(item.code).includes(keyword),
            )
          : activeItems;

        const selectedCount = activeItems.filter((item) =>
          selectedCodes.has(item.code),
        ).length;

        return {
          groupKey: group.groupKey,
          groupLabel: group.groupLabel,
          items: visibleItems,
          totalCount: activeItems.length,
          selectedCount,
          allSelected:
            activeItems.length > 0 && selectedCount === activeItems.length,
          partiallySelected:
            selectedCount > 0 && selectedCount < activeItems.length,
        };
      })
      .filter((group) => group.items.length > 0);
  }

  closeDialog(): void {
    this.visibleChange.emit(false);
  }

  onDialogHide(): void {
    this.closeDialog();
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
      if (controlName === 'moduleCodes') {
        return 'Vui lòng chọn ít nhất một module';
      }

      return 'Trường này là bắt buộc';
    }

    if (control.errors['maxlength']) {
      return 'Dữ liệu vượt quá độ dài cho phép';
    }

    return 'Dữ liệu không hợp lệ';
  }

  isModuleSelected(code: string): boolean {
    return (this.form.controls.moduleCodes.value ?? []).includes(code);
  }

  isGroupChecked(group: ModuleGroupViewModel): boolean {
    return group.allSelected;
  }

  isGroupIndeterminate(group: ModuleGroupViewModel): boolean {
    return group.partiallySelected && !group.allSelected;
  }

  onGroupSelectionChange(
    group: MauBaoCaoModuleCatalogGroupDto,
    checked: boolean | undefined,
  ): void {
    const current = new Set(this.form.controls.moduleCodes.value ?? []);
    const activeCodes = group.items
      .filter((item) => item.isActive)
      .map((item) => item.code);

    if (checked) {
      this.form.controls.moduleCodes.setValue(
        Array.from(new Set([...current, ...activeCodes])),
      );
      return;
    }

    this.form.controls.moduleCodes.setValue(
      Array.from(current).filter((code) => !activeCodes.includes(code)),
    );
  }

  onGroupSelectionChangeByKey(
    groupKey: string,
    checked: boolean | undefined,
  ): void {
    const group = this.moduleCatalogGroups.find(
      (catalogGroup) => catalogGroup.groupKey === groupKey,
    );

    if (!group) {
      return;
    }

    this.onGroupSelectionChange(group, checked);
  }

  onModuleSelectionChange(code: string, checked: boolean | undefined): void {
    const current = new Set(this.form.controls.moduleCodes.value ?? []);

    if (checked) {
      current.add(code);
    } else {
      current.delete(code);
    }

    this.form.controls.moduleCodes.setValue(Array.from(current));
  }

  trackGroup(_index: number, group: ModuleGroupViewModel): string {
    return group.groupKey;
  }

  trackModule(_index: number, item: ModuleGroupOption): string {
    return item.code;
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
      maMau: (raw.maMau ?? '').trim(),
      tenMau: (raw.tenMau ?? '').trim(),
      tanSuat: raw.tanSuat ?? 2,
      moduleCodes: raw.moduleCodes ?? [],
      moTa: (raw.moTa ?? '').trim() || null,
      isActive: !!raw.isActive,
    });
  }

  private normalizeKeyword(value: string): string {
    return value.trim().toLocaleLowerCase('vi-VN');
  }

  private rebuildModuleViewModel(): void {
    this.filteredModuleGroups = this.buildFilteredModuleGroups();

    if (!this.filteredModuleGroups.length) {
      this.expandedGroupKeys.clear();
      return;
    }

    const visibleKeys = new Set(
      this.filteredModuleGroups.map((group) => group.groupKey),
    );

    this.expandedGroupKeys.forEach((groupKey) => {
      if (!visibleKeys.has(groupKey)) {
        this.expandedGroupKeys.delete(groupKey);
      }
    });
  }
}
