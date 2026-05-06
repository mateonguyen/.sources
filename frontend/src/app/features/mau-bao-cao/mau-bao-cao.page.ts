import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DropdownModule } from 'primeng/dropdown';
import { InputTextModule } from 'primeng/inputtext';
import { PaginatorModule, PaginatorState } from 'primeng/paginator';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ConfirmDialogWrapperService } from '../../shared/ui/confirm-dialog-wrapper.service';
import { EmptyStateComponent } from '../../shared/ui/empty-state.component';
import { LoadingOverlayComponent } from '../../shared/ui/loading-overlay.component';
import { SectionCardComponent } from '../../shared/ui/section-card.component';
import { NotificationService } from '../../core/ui/notification.service';
import {
  MauBaoCaoApi,
  MauBaoCaoDto,
  MauBaoCaoModuleCatalogGroupDto,
  MauBaoCaoModuleCatalogItemDto,
  TanSuat,
  UpsertMauBaoCaoRequest,
} from './mau-bao-cao.api';
import {
  MauBaoCaoDialogInitialData,
  MauBaoCaoDialogMode,
  MauBaoCaoDialogSubmitPayload,
  MauBaoCaoUpsertDialogComponent,
} from './mau-bao-cao-upsert-dialog.component';

@Component({
  selector: 'app-mau-bao-cao-page',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    SectionCardComponent,
    EmptyStateComponent,
    LoadingOverlayComponent,
    ButtonModule,
    InputTextModule,
    DropdownModule,
    PaginatorModule,
    TableModule,
    TagModule,
    MauBaoCaoUpsertDialogComponent,
  ],
  templateUrl: './mau-bao-cao.page.html',
  styleUrl: './mau-bao-cao.page.scss',
})
export class MauBaoCaoPage {
  items: MauBaoCaoDto[] = [];
  moduleCatalogGroups: MauBaoCaoModuleCatalogGroupDto[] = [];
  searchTerm = '';
  selectedTanSuat: TanSuat | null = null;
  selectedStatus: 'active' | 'inactive' | null = null;
  loading = false;
  saving = false;
  apiError = '';
  first = 0;
  rows = 10;
  dialogVisible = false;
  dialogMode: MauBaoCaoDialogMode = 'create';
  dialogInitialData: MauBaoCaoDialogInitialData | null = null;

  readonly tanSuatFilterOptions = [
    { label: 'Tất cả tần suất', value: null },
    { label: 'Tháng', value: 1 as TanSuat },
    { label: 'Quý', value: 2 as TanSuat },
    { label: 'Năm', value: 3 as TanSuat },
  ];

  readonly statusFilterOptions = [
    { label: 'Tất cả trạng thái', value: null },
    { label: 'Đang hoạt động', value: 'active' as const },
    { label: 'Ngừng hoạt động', value: 'inactive' as const },
  ];

  readonly pageSizeDropdownOptions = [10, 20, 50].map((value) => ({
    label: String(value),
    value,
  }));

  constructor(
    private readonly mauBaoCaoApi: MauBaoCaoApi,
    private readonly notificationService: NotificationService,
    private readonly confirmDialog: ConfirmDialogWrapperService,
  ) {
    this.load();
  }

  async load(): Promise<void> {
    this.loading = true;
    this.apiError = '';

    try {
      const [items, moduleCatalogGroups] = await Promise.all([
        this.mauBaoCaoApi.getAll(true),
        this.mauBaoCaoApi.getModuleCatalog(),
      ]);

      this.items = items;
      this.moduleCatalogGroups = moduleCatalogGroups;
    } catch (error: unknown) {
      this.apiError = this.resolveErrorMessage(
        error,
        'Không thể tải danh sách mẫu báo cáo.',
      );
    } finally {
      this.loading = false;
    }
  }

  openCreate(): void {
    this.dialogMode = 'create';
    this.dialogInitialData = {
      maMau: '',
      tenMau: '',
      tanSuat: 2,
      moduleCodes: [],
      moTa: '',
      isActive: true,
    };
    this.dialogVisible = true;
  }

  openEdit(item: MauBaoCaoDto): void {
    this.dialogMode = 'edit';
    this.dialogInitialData = {
      id: item.id,
      maMau: item.maMau,
      tenMau: item.tenMau,
      tanSuat: item.tanSuat,
      moduleCodes: [...item.danhSachModule],
      moTa: item.moTa ?? '',
      isActive: item.isActive,
    };
    this.dialogVisible = true;
  }

  onDialogVisibleChange(visible: boolean): void {
    this.dialogVisible = visible;
    if (!visible) {
      this.dialogInitialData = null;
    }
  }

  async onDialogSave(payload: MauBaoCaoDialogSubmitPayload): Promise<void> {
    this.saving = true;

    try {
      const request: UpsertMauBaoCaoRequest = {
        maMau: payload.maMau,
        tenMau: payload.tenMau,
        danhSachModule: payload.moduleCodes,
        tanSuat: payload.tanSuat,
        moTa: payload.moTa ?? undefined,
        isActive: payload.isActive,
      };

      if (payload.mode === 'edit' && payload.id) {
        await this.mauBaoCaoApi.update(payload.id, request);
        this.notificationService.show(
          'success',
          'Cập nhật mẫu báo cáo thành công.',
        );
      } else {
        await this.mauBaoCaoApi.create(request);
        this.notificationService.show(
          'success',
          'Thêm mẫu báo cáo thành công.',
        );
      }

      this.dialogVisible = false;
      this.dialogInitialData = null;
      await this.load();
    } catch (error: unknown) {
      this.notificationService.show(
        'error',
        this.resolveErrorMessage(error, 'Không thể lưu mẫu báo cáo.'),
      );
    } finally {
      this.saving = false;
    }
  }

  async delete(item: MauBaoCaoDto): Promise<void> {
    const confirmed = await this.confirmDialog.confirmDelete({
      header: 'Xóa mẫu báo cáo',
      message: `Xác nhận xóa mẫu '${item.tenMau}' (${item.maMau})?`,
      acceptLabel: 'Xóa',
      rejectLabel: 'Hủy',
    });

    if (!confirmed) {
      return;
    }

    await this.mauBaoCaoApi.delete(item.id);
    this.notificationService.show('success', 'Xóa mẫu báo cáo thành công.');
    await this.load();
  }

  tanSuatLabel(value: TanSuat): string {
    if (value === 1) {
      return 'Tháng';
    }
    if (value === 2) {
      return 'Quý';
    }
    return 'Năm';
  }

  get filteredItems(): MauBaoCaoDto[] {
    const normalizedSearch = this.normalize(this.searchTerm);

    return this.items.filter((item) => {
      const matchesSearch =
        !normalizedSearch ||
        this.normalize(
          [
            item.maMau,
            item.tenMau,
            item.moTa ?? '',
            ...this.moduleLabels(item.danhSachModule),
          ].join(' '),
        ).includes(normalizedSearch);

      const matchesTanSuat =
        this.selectedTanSuat == null || item.tanSuat === this.selectedTanSuat;

      const matchesStatus =
        this.selectedStatus == null ||
        (this.selectedStatus === 'active' ? item.isActive : !item.isActive);

      return matchesSearch && matchesTanSuat && matchesStatus;
    });
  }

  get pagedItems(): MauBaoCaoDto[] {
    return this.filteredItems.slice(this.first, this.first + this.rows);
  }

  get pagedSummary(): string {
    if (this.filteredItems.length === 0) {
      return 'Không có mẫu báo cáo phù hợp';
    }

    const start = this.first + 1;
    const end = Math.min(this.first + this.rows, this.filteredItems.length);
    return `Hiển thị ${start}-${end} trong tổng số ${this.filteredItems.length} mẫu báo cáo`;
  }

  onFiltersChanged(): void {
    this.first = 0;
  }

  onPageChange(event: PaginatorState): void {
    this.first = event.first ?? 0;
    this.rows = event.rows ?? this.rows;
  }

  onRowsChange(): void {
    this.first = 0;
  }

  resetFilters(): void {
    this.searchTerm = '';
    this.selectedTanSuat = null;
    this.selectedStatus = null;
    this.first = 0;
  }

  moduleLabels(codes: readonly string[]): string[] {
    return codes.map((code) => this.moduleLabel(code));
  }

  moduleLabel(code: string): string {
    const item = this.findModuleItem(code);
    return item?.label ?? code;
  }

  private findModuleItem(
    code: string,
  ): MauBaoCaoModuleCatalogItemDto | undefined {
    for (const group of this.moduleCatalogGroups) {
      const item = group.items.find(
        (moduleItem) => moduleItem.code === code && moduleItem.isActive,
      );
      if (item) {
        return item;
      }
    }

    return undefined;
  }

  private resolveErrorMessage(error: unknown, fallback: string): string {
    if (error instanceof Error && error.message) {
      return error.message;
    }

    const httpError = error as {
      error?: {
        error?: { message?: string; Message?: string };
        Error?: { message?: string; Message?: string };
        message?: string;
        Message?: string;
      };
    };

    return (
      httpError.error?.error?.message ??
      httpError.error?.error?.Message ??
      httpError.error?.Error?.message ??
      httpError.error?.Error?.Message ??
      httpError.error?.message ??
      httpError.error?.Message ??
      fallback
    );
  }

  private normalize(value: string): string {
    return value.trim().toLocaleLowerCase('vi-VN');
  }
}
