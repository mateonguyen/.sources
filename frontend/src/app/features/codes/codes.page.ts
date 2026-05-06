import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DropdownModule } from 'primeng/dropdown';
import { InputTextModule } from 'primeng/inputtext';
import { PaginatorModule, PaginatorState } from 'primeng/paginator';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import {
  CodeDto,
  CodeValueDto,
  CodesApi,
  UpsertCodeRequest,
  UpsertCodeValueRequest,
} from './codes.api';
import { ConfirmDialogWrapperService } from '../../shared/ui/confirm-dialog-wrapper.service';
import { EmptyStateComponent } from '../../shared/ui/empty-state.component';
import { LoadingOverlayComponent } from '../../shared/ui/loading-overlay.component';
import { SectionCardComponent } from '../../shared/ui/section-card.component';
import { NotificationService } from '../../core/ui/notification.service';
import {
  CategoryUpsertDialogComponent,
  CategoryUpsertDialogMode,
  CategoryUpsertInitialData,
  CategoryUpsertSubmitPayload,
} from './category-upsert-dialog.component';
import {
  CodeValueDialogComponent,
  CodeValueDialogMode,
  CodeValueInitialData,
  CodeValueSubmitPayload,
} from './code-value-dialog.component';

type CategorySelectorValue = number | null;

interface CategorySelectorItem {
  label: string;
  value: number;
}

@Component({
  selector: 'app-codes-page',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    SectionCardComponent,
    EmptyStateComponent,
    LoadingOverlayComponent,
    InputTextModule,
    DropdownModule,
    ButtonModule,
    TableModule,
    PaginatorModule,
    TagModule,
    CategoryUpsertDialogComponent,
    CodeValueDialogComponent,
  ],
  templateUrl: './codes.page.html',
  styleUrl: './codes.page.scss',
})
export class CodesPage {
  data: CodeDto[] = [];
  selectedCode: CodeDto | null = null;

  loading = false;
  savingValue = false;
  apiError = '';

  categorySelectorValue: CategorySelectorValue = null;
  categorySelectorOptions: CategorySelectorItem[] = [];

  categoryDialogVisible = false;
  categoryDialogSubmitting = false;
  categoryDialogMode: CategoryUpsertDialogMode = 'create';
  categoryDialogInitialData: CategoryUpsertInitialData | null = null;

  valueSearchTerm = '';
  valueDialogVisible = false;
  valueDialogMode: CodeValueDialogMode = 'create';
  valueDialogInitialData: CodeValueInitialData | null = null;
  valueFirst = 0;
  valueRows = 10;

  readonly valuePageSizeOptions = [10, 20, 50];

  constructor(
    private readonly codesApi: CodesApi,
    private readonly notificationService: NotificationService,
    private readonly confirmDialog: ConfirmDialogWrapperService,
  ) {
    this.load();
  }

  private buildCategorySelectorOptions(): CategorySelectorItem[] {
    return this.data.map((item) => ({
      label: item.name,
      value: item.id,
    }));
  }

  get filteredValues(): CodeValueDto[] {
    const values = this.selectedCode?.values ?? [];
    const keyword = this.normalize(this.valueSearchTerm);
    if (!keyword) {
      return values;
    }

    return values.filter((item) => {
      const haystack = [item.value, item.name, item.description ?? '']
        .map((part) => this.normalize(part))
        .join(' ');
      return haystack.includes(keyword);
    });
  }

  get pagedValues(): CodeValueDto[] {
    return this.filteredValues.slice(
      this.valueFirst,
      this.valueFirst + this.valueRows,
    );
  }

  get totalValueCount(): number {
    return this.selectedCode?.values.length ?? 0;
  }

  get valueToolbarCount(): string {
    if (!this.selectedCode) {
      return '';
    }

    if (this.filteredValues.length === this.totalValueCount) {
      return `${this.totalValueCount} giá trị`;
    }

    return `${this.filteredValues.length}/${this.totalValueCount} giá trị`;
  }

  get valuePageSizeDropdownOptions(): Array<{ label: string; value: number }> {
    return this.valuePageSizeOptions.map((value) => ({
      label: `${value}`,
      value,
    }));
  }

  get valueSummary(): string {
    if (!this.selectedCode) {
      return 'Chọn một danh mục để xem các giá trị.';
    }

    if (this.filteredValues.length === 0) {
      return 'Không có giá trị danh mục phù hợp.';
    }

    const start = this.valueFirst + 1;
    const end = Math.min(
      this.valueFirst + this.valueRows,
      this.filteredValues.length,
    );
    return `Hiển thị ${start}–${end} trong tổng số ${this.filteredValues.length} giá trị`;
  }

  async load(preferredCodeId?: number): Promise<void> {
    this.loading = true;
    this.apiError = '';

    try {
      this.data = await this.codesApi.getAll();
      const targetId =
        preferredCodeId ?? this.selectedCode?.id ?? this.data[0]?.id;

      if (targetId) {
        await this.selectCode(targetId);
      } else {
        this.resetSelectionState();
      }

      this.categorySelectorOptions = this.buildCategorySelectorOptions();
    } catch (error: unknown) {
      this.apiError =
        (error as { error?: { error?: { message?: string } } })?.error?.error
          ?.message ?? 'Không thể tải dữ liệu danh mục.';
      this.resetSelectionState();
    } finally {
      this.loading = false;
    }
  }

  async selectCode(id: number): Promise<void> {
    this.selectedCode = await this.codesApi.getById(id);
    this.categorySelectorValue = id;
    this.valueSearchTerm = '';
    this.valueFirst = 0;
    this.categorySelectorOptions = this.buildCategorySelectorOptions();
  }

  async onCategorySelectionChange(value: CategorySelectorValue): Promise<void> {
    if (value === null || value === undefined) {
      this.categorySelectorValue = this.selectedCode?.id ?? null;
      return;
    }

    await this.selectCode(value);
  }

  openCreateCategoryDialog(): void {
    this.apiError = '';
    this.categoryDialogMode = 'create';
    this.categoryDialogInitialData = {
      code: '',
      name: '',
      description: '',
      sortOrder: this.data.length + 1,
      isActive: true,
    };
    this.categoryDialogVisible = true;
  }

  openEditCategoryDialog(): void {
    if (!this.selectedCode) {
      this.notificationService.show(
        'info',
        'Vui lòng chọn danh mục để cập nhật.',
      );
      return;
    }

    this.categoryDialogMode = 'edit';
    this.categoryDialogInitialData = {
      id: this.selectedCode.id,
      code: this.selectedCode.code,
      name: this.selectedCode.name,
      description: this.selectedCode.description ?? '',
      sortOrder: this.selectedCode.sortOrder,
      isActive: this.selectedCode.isActive,
    };
    this.categoryDialogVisible = true;
  }

  onCategoryDialogVisibleChange(visible: boolean): void {
    this.categoryDialogVisible = visible;
    if (!visible) {
      this.categoryDialogInitialData = null;
    }
  }

  async onCategoryDialogSave(
    payload: CategoryUpsertSubmitPayload,
  ): Promise<void> {
    this.categoryDialogSubmitting = true;
    this.apiError = '';

    try {
      const request: UpsertCodeRequest = {
        code: payload.code,
        name: payload.name,
        description: payload.description ?? undefined,
        sortOrder: payload.sortOrder,
        isActive: payload.isActive,
      };

      let selectedId: number;
      if (payload.mode === 'edit' && payload.id) {
        const updated = await this.codesApi.update(payload.id, request);
        selectedId = updated.id;
        this.notificationService.show(
          'success',
          'Cập nhật danh mục thành công.',
        );
      } else {
        const created = await this.codesApi.create(request);
        selectedId = created.id;
        this.notificationService.show('success', 'Thêm danh mục thành công.');
      }

      this.categoryDialogVisible = false;
      this.categoryDialogInitialData = null;
      await this.load(selectedId);
    } catch (error: unknown) {
      this.apiError =
        (error as { error?: { error?: { message?: string } } })?.error?.error
          ?.message ?? 'Không thể lưu danh mục.';
    } finally {
      this.categoryDialogSubmitting = false;
    }
  }

  async deleteCode(): Promise<void> {
    if (!this.selectedCode) {
      return;
    }

    const valueCount = this.selectedCode.values.length;
    const warningText =
      valueCount > 0
        ? `Danh mục này hiện có ${valueCount} giá trị. Hành động này có thể ảnh hưởng dữ liệu liên quan.`
        : 'Danh mục này chưa có giá trị liên kết.';

    const confirmed = await this.confirmDialog.confirmDelete({
      message: `Xác nhận xóa danh mục "${this.selectedCode.name}"? ${warningText}`,
      acceptLabel: 'Xóa danh mục',
      rejectLabel: 'Hủy',
    });

    if (!confirmed) {
      return;
    }

    await this.codesApi.delete(this.selectedCode.id);
    this.notificationService.show('success', 'Xóa danh mục thành công.');
    await this.load();
  }

  onValueSearchChange(): void {
    this.valueFirst = 0;
  }

  clearValueSearch(): void {
    this.valueSearchTerm = '';
    this.onValueSearchChange();
  }

  onValuePageChange(event: PaginatorState): void {
    this.valueFirst = event.first ?? 0;
    this.valueRows = event.rows ?? this.valueRows;
  }

  onValueRowsChange(): void {
    this.valueFirst = 0;
  }

  openCreateValueDialog(): void {
    if (!this.selectedCode) {
      this.notificationService.show(
        'info',
        'Vui lòng chọn danh mục trước khi thêm giá trị.',
      );
      return;
    }

    this.valueDialogMode = 'create';
    this.valueDialogInitialData = {
      value: '',
      name: '',
      description: '',
      sortOrder: 0,
      isActive: true,
    };
    this.valueDialogVisible = true;
  }

  openEditValueDialog(item: CodeValueDto): void {
    this.valueDialogMode = 'edit';
    this.valueDialogInitialData = {
      id: item.id,
      value: item.value,
      name: item.name,
      description: item.description ?? '',
      sortOrder: item.sortOrder,
      isActive: item.isActive,
    };
    this.valueDialogVisible = true;
  }

  onValueDialogVisibleChange(visible: boolean): void {
    this.valueDialogVisible = visible;
    if (!visible) {
      this.valueDialogInitialData = null;
    }
  }

  async onValueDialogSave(payload: CodeValueSubmitPayload): Promise<void> {
    if (!this.selectedCode) {
      return;
    }

    this.savingValue = true;
    try {
      const normalizedSortOrder = this.normalizeSortOrder(payload.sortOrder);

      if (payload.mode === 'edit' && payload.id) {
        const finalSortOrder = await this.prepareSortOrderForEdit(
          this.selectedCode.id,
          payload.id,
          normalizedSortOrder,
        );

        await this.codesApi.updateValue(
          this.selectedCode.id,
          payload.id,
          this.buildValueRequest(payload, finalSortOrder),
        );
        this.notificationService.show(
          'success',
          'Cập nhật giá trị danh mục thành công.',
        );
      } else {
        await this.prepareSortOrderForCreate(
          this.selectedCode.id,
          normalizedSortOrder,
        );

        await this.codesApi.createValue(
          this.selectedCode.id,
          this.buildValueRequest(payload, normalizedSortOrder),
        );
        this.notificationService.show(
          'success',
          'Thêm giá trị danh mục thành công.',
        );
      }

      await this.selectCode(this.selectedCode.id);
      this.valueDialogVisible = false;
      this.valueDialogInitialData = null;
    } catch (error) {
      if (this.selectedCode) {
        await this.selectCode(this.selectedCode.id);
      }
      throw error;
    } finally {
      this.savingValue = false;
    }
  }

  async deleteValue(item: CodeValueDto): Promise<void> {
    if (!this.selectedCode) {
      return;
    }

    const confirmed = await this.confirmDialog.confirmDelete({
      message: `Xác nhận xóa giá trị ${item.name}?`,
      acceptLabel: 'Xóa giá trị',
      rejectLabel: 'Hủy',
    });

    if (!confirmed) {
      return;
    }

    await this.codesApi.deleteValue(this.selectedCode.id, item.id);
    this.notificationService.show(
      'success',
      'Xóa giá trị danh mục thành công.',
    );
    await this.selectCode(this.selectedCode.id);
  }

  calculateValueStt(index: number): number {
    return this.valueFirst + index + 1;
  }

  private resetSelectionState(): void {
    this.selectedCode = null;
    this.categorySelectorValue = null;
    this.categorySelectorOptions = this.buildCategorySelectorOptions();
    this.valueSearchTerm = '';
    this.valueFirst = 0;
  }

  private buildValueRequest(
    payload: CodeValueSubmitPayload,
    sortOrder: number,
  ): UpsertCodeValueRequest {
    return {
      value: payload.value,
      name: payload.name,
      description: payload.description ?? undefined,
      sortOrder,
      isActive: payload.isActive,
    };
  }

  private normalizeSortOrder(value: number | null | undefined): number {
    return Math.max(0, Math.trunc(Number(value ?? 0)));
  }

  private async prepareSortOrderForCreate(
    codeId: number,
    desiredSortOrder: number,
  ): Promise<void> {
    const currentValues = [...(this.selectedCode?.values ?? [])];
    const affected = currentValues
      .filter((item) => item.sortOrder >= desiredSortOrder)
      .sort((left, right) =>
        right.sortOrder !== left.sortOrder
          ? right.sortOrder - left.sortOrder
          : right.id - left.id,
      );

    for (const item of affected) {
      await this.updateExistingValueSortOrder(codeId, item, item.sortOrder + 1);
    }
  }

  private async prepareSortOrderForEdit(
    codeId: number,
    valueId: number,
    desiredSortOrder: number,
  ): Promise<number> {
    const currentValues = [...(this.selectedCode?.values ?? [])];
    const currentItem = currentValues.find((item) => item.id === valueId);

    if (!currentItem) {
      return desiredSortOrder;
    }

    const currentSortOrder = currentItem.sortOrder;
    if (currentSortOrder === desiredSortOrder) {
      return desiredSortOrder;
    }

    const temporarySortOrder =
      Math.max(
        desiredSortOrder,
        currentSortOrder,
        ...currentValues.map((item) => item.sortOrder),
      ) + 1000;

    await this.updateExistingValueSortOrder(
      codeId,
      currentItem,
      temporarySortOrder,
    );

    if (desiredSortOrder > currentSortOrder) {
      const affected = currentValues
        .filter(
          (item) =>
            item.id !== valueId &&
            item.sortOrder > currentSortOrder &&
            item.sortOrder <= desiredSortOrder,
        )
        .sort((left, right) =>
          left.sortOrder !== right.sortOrder
            ? left.sortOrder - right.sortOrder
            : left.id - right.id,
        );

      for (const item of affected) {
        await this.updateExistingValueSortOrder(
          codeId,
          item,
          item.sortOrder - 1,
        );
      }
    } else {
      const affected = currentValues
        .filter(
          (item) =>
            item.id !== valueId &&
            item.sortOrder >= desiredSortOrder &&
            item.sortOrder < currentSortOrder,
        )
        .sort((left, right) =>
          left.sortOrder !== right.sortOrder
            ? right.sortOrder - left.sortOrder
            : right.id - left.id,
        );

      for (const item of affected) {
        await this.updateExistingValueSortOrder(
          codeId,
          item,
          item.sortOrder + 1,
        );
      }
    }

    return desiredSortOrder;
  }

  private updateExistingValueSortOrder(
    codeId: number,
    item: CodeValueDto,
    sortOrder: number,
  ): Promise<CodeValueDto> {
    return this.codesApi.updateValue(codeId, item.id, {
      value: item.value,
      name: item.name,
      description: item.description ?? undefined,
      sortOrder,
      isActive: item.isActive,
    });
  }

  private normalize(value: string | null | undefined): string {
    return (value ?? '')
      .normalize('NFD')
      .replace(/\p{Diacritic}/gu, '')
      .trim()
      .toLowerCase();
  }
}
