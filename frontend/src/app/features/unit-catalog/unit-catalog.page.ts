import { CommonModule } from '@angular/common';
import { Component, Input, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { DropdownModule } from 'primeng/dropdown';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { InputTextareaModule } from 'primeng/inputtextarea';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { AuthService } from '../../core/auth/auth.service';
import { NotificationService } from '../../core/ui/notification.service';
import { ConfirmDialogWrapperService } from '../../shared/ui/confirm-dialog-wrapper.service';
import { EmptyStateComponent } from '../../shared/ui/empty-state.component';
import { LoadingOverlayComponent } from '../../shared/ui/loading-overlay.component';
import {
  CodeDto,
  CodeValueDto,
  CodesApi,
  UpsertUnitCodeValueRequest,
} from '../codes/codes.api';
import {
  RefLoaiThietBiApi,
  RefLoaiThietBiDto,
  UpsertUnitRefLoaiThietBiRequest,
} from '../ref-loai-thiet-bi/ref-loai-thiet-bi.api';

type CatalogTab = 'common' | 'device';
type DialogMode = 'create' | 'edit';

interface DeviceRow extends RefLoaiThietBiDto {
  depth: number;
}

@Component({
  selector: 'app-unit-catalog-page',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ButtonModule,
    DialogModule,
    DropdownModule,
    InputNumberModule,
    InputTextModule,
    InputTextareaModule,
    TableModule,
    TagModule,
    TooltipModule,
    EmptyStateComponent,
    LoadingOverlayComponent,
  ],
  templateUrl: './unit-catalog.page.html',
  styleUrl: './unit-catalog.page.scss',
})
export class UnitCatalogPage implements OnInit {
  @Input() catalogType: CatalogTab = 'common';

  activeTab: CatalogTab = 'common';
  loading = false;
  saving = false;
  searchTerm = '';

  catalogs: CodeDto[] = [];
  selectedCodeId: number | null = null;
  codeValues: CodeValueDto[] = [];

  deviceTree: RefLoaiThietBiDto[] = [];
  deviceRows: DeviceRow[] = [];

  commonDialogVisible = false;
  commonDialogMode: DialogMode = 'create';
  commonEditingId: number | null = null;
  commonForm = {
    name: '',
    description: '',
    sortOrder: 0,
  };

  deviceDialogVisible = false;
  deviceDialogMode: DialogMode = 'create';
  deviceEditingId: number | null = null;
  deviceForm = {
    parentId: null as number | null,
    tenLoai: '',
    laTongHop: false,
    sortOrder: 0,
  };

  constructor(
    private readonly codesApi: CodesApi,
    private readonly deviceApi: RefLoaiThietBiApi,
    private readonly authService: AuthService,
    private readonly notificationService: NotificationService,
    private readonly confirmDialog: ConfirmDialogWrapperService,
  ) {}

  ngOnInit(): void {
    this.activeTab = this.catalogType;
    void this.load();
  }

  get selectedCode(): CodeDto | null {
    return this.catalogs.find((x) => x.id === this.selectedCodeId) ?? null;
  }

  get filteredCodeValues(): CodeValueDto[] {
    const keyword = this.normalize(this.searchTerm);
    if (!keyword) return this.codeValues;
    return this.codeValues.filter((x) =>
      this.normalize(`${x.name} ${x.description ?? ''}`).includes(keyword),
    );
  }

  get filteredDeviceRows(): DeviceRow[] {
    const keyword = this.normalize(this.searchTerm);
    if (!keyword) return this.deviceRows;
    return this.deviceRows.filter((x) =>
      this.normalize(`${x.tenLoai} ${x.maLoai}`).includes(keyword),
    );
  }

  get deviceParentOptions(): Array<{ label: string; value: number | null }> {
    return [
      { label: 'Không có - tạo nhóm gốc', value: null },
      ...this.deviceRows
        .filter((x) => x.id !== this.deviceEditingId)
        .map((x) => ({
          label: `${'— '.repeat(x.depth)}${x.tenLoai}`,
          value: x.id,
        })),
    ];
  }

  get canCreateDevice(): boolean {
    return this.authService.hasPermission('ref_loai_thiet_bi:create');
  }

  get canCreateCatalog(): boolean {
    return this.authService.hasPermission('codes:create');
  }

  get canUpdateCatalog(): boolean {
    return this.authService.hasPermission('codes:update');
  }

  get canDeleteCatalog(): boolean {
    return this.authService.hasPermission('codes:delete');
  }

  get canReadDevice(): boolean {
    return this.authService.hasPermission('ref_loai_thiet_bi:read');
  }

  get canUpdateDevice(): boolean {
    return this.authService.hasPermission('ref_loai_thiet_bi:update');
  }

  get canDeleteDevice(): boolean {
    return this.authService.hasPermission('ref_loai_thiet_bi:delete');
  }

  async load(): Promise<void> {
    this.loading = true;
    try {
      if (this.activeTab === 'common') {
        const allCodes = await this.codesApi.getAll();
        this.catalogs = allCodes.filter((x) => x.allowUnitCustomization);
        if (!this.selectedCodeId || !this.catalogs.some((x) => x.id === this.selectedCodeId)) {
          this.selectedCodeId = this.catalogs[0]?.id ?? null;
        }
        await this.loadSelectedCodeValues();
      } else if (this.canReadDevice) {
        await this.reloadDeviceTree();
      }
    } catch (error: unknown) {
      this.notificationService.show(
        'error',
        this.extractError(error, 'Không thể tải danh mục của đơn vị.'),
      );
    } finally {
      this.loading = false;
    }
  }

  async onCodeChange(): Promise<void> {
    this.searchTerm = '';
    await this.loadSelectedCodeValues();
  }

  private async loadSelectedCodeValues(): Promise<void> {
    this.codeValues = this.selectedCodeId
      ? await this.codesApi.getUnitValues(this.selectedCodeId, true)
      : [];
  }

  private async reloadDeviceTree(): Promise<void> {
    this.deviceTree = await this.deviceApi.getUnitTree(true);
    this.deviceRows = this.flattenDeviceTree(this.deviceTree);
  }

  openCreateCommonDialog(): void {
    if (!this.selectedCodeId) return;
    this.commonDialogMode = 'create';
    this.commonEditingId = null;
    this.commonForm = {
      name: '',
      description: '',
      sortOrder: this.codeValues.length + 1,
    };
    this.commonDialogVisible = true;
  }

  openEditCommonDialog(item: CodeValueDto): void {
    this.commonDialogMode = 'edit';
    this.commonEditingId = item.id;
    this.commonForm = {
      name: item.name,
      description: item.description ?? '',
      sortOrder: item.sortOrder,
    };
    this.commonDialogVisible = true;
  }

  async saveCommonValue(): Promise<void> {
    if (!this.selectedCodeId || !this.commonForm.name.trim()) {
      this.notificationService.show('warning', 'Vui lòng nhập tên giá trị danh mục.');
      return;
    }
    const payload: UpsertUnitCodeValueRequest = {
      name: this.commonForm.name.trim(),
      description: this.commonForm.description.trim() || undefined,
      sortOrder: Number(this.commonForm.sortOrder ?? 0),
      isActive: true,
    };
    this.saving = true;
    try {
      if (this.commonDialogMode === 'edit' && this.commonEditingId) {
        await this.codesApi.updateUnitValue(
          this.selectedCodeId,
          this.commonEditingId,
          payload,
        );
      } else {
        await this.codesApi.createUnitValue(this.selectedCodeId, payload);
      }
      this.commonDialogVisible = false;
      await this.loadSelectedCodeValues();
      this.notificationService.show('success', 'Đã lưu mục bổ sung của đơn vị.');
    } catch (error: unknown) {
      this.notificationService.show('error', this.extractError(error, 'Không thể lưu danh mục.'));
    } finally {
      this.saving = false;
    }
  }

  async toggleCommonVisibility(item: CodeValueDto): Promise<void> {
    if (!this.selectedCodeId || item.isLocal || item.isRequired) return;
    this.saving = true;
    try {
      await this.codesApi.setUnitValueVisibility(
        this.selectedCodeId,
        item.id,
        !(item.isVisible ?? true),
      );
      await this.loadSelectedCodeValues();
    } catch (error: unknown) {
      this.notificationService.show('error', this.extractError(error, 'Không thể đổi trạng thái áp dụng.'));
    } finally {
      this.saving = false;
    }
  }

  async deleteCommonValue(item: CodeValueDto): Promise<void> {
    if (!this.selectedCodeId || !item.isLocal) return;
    const accepted = await this.confirmDialog.confirmDeactivate({
      message: `Ngừng áp dụng mục bổ sung "${item.name}"?`,
      acceptLabel: 'Ngừng áp dụng',
      rejectLabel: 'Hủy',
    });
    if (!accepted) return;
    this.saving = true;
    try {
      await this.codesApi.deleteUnitValue(this.selectedCodeId, item.id);
      await this.loadSelectedCodeValues();
      this.notificationService.show('success', 'Đã ngừng áp dụng mục bổ sung.');
    } catch (error: unknown) {
      this.notificationService.show('error', this.extractError(error, 'Không thể ngừng áp dụng mục bổ sung.'));
    } finally {
      this.saving = false;
    }
  }

  openCreateDeviceDialog(parent?: RefLoaiThietBiDto): void {
    this.deviceDialogMode = 'create';
    this.deviceEditingId = null;
    this.deviceForm = {
      parentId: parent?.id ?? null,
      tenLoai: '',
      laTongHop: false,
      sortOrder: parent?.children.length ?? this.deviceTree.length,
    };
    this.deviceDialogVisible = true;
  }

  openEditDeviceDialog(item: DeviceRow): void {
    this.deviceDialogMode = 'edit';
    this.deviceEditingId = item.id;
    this.deviceForm = {
      parentId: item.parentId,
      tenLoai: item.tenLoai,
      laTongHop: item.laTongHop,
      sortOrder: item.sortOrder,
    };
    this.deviceDialogVisible = true;
  }

  async saveDeviceValue(): Promise<void> {
    if (!this.deviceForm.tenLoai.trim()) {
      this.notificationService.show('warning', 'Vui lòng nhập tên loại thiết bị.');
      return;
    }
    const payload: UpsertUnitRefLoaiThietBiRequest = {
      parentId: this.deviceForm.parentId,
      tenLoai: this.deviceForm.tenLoai.trim(),
      laTongHop: this.deviceForm.laTongHop,
      sortOrder: Number(this.deviceForm.sortOrder ?? 0),
      isActive: true,
    };
    this.saving = true;
    try {
      if (this.deviceDialogMode === 'edit' && this.deviceEditingId) {
        await this.deviceApi.updateUnitItem(this.deviceEditingId, payload);
      } else {
        await this.deviceApi.createUnitItem(payload);
      }
      this.deviceDialogVisible = false;
      await this.reloadDeviceTree();
      this.notificationService.show('success', 'Đã lưu loại thiết bị riêng của đơn vị.');
    } catch (error: unknown) {
      this.notificationService.show('error', this.extractError(error, 'Không thể lưu loại thiết bị.'));
    } finally {
      this.saving = false;
    }
  }

  async toggleDeviceVisibility(item: DeviceRow): Promise<void> {
    if (item.isLocal || item.isRequired || !this.canUpdateDevice) return;
    this.saving = true;
    try {
      await this.deviceApi.setUnitItemVisibility(item.id, !(item.isVisible ?? true));
      await this.reloadDeviceTree();
    } catch (error: unknown) {
      this.notificationService.show('error', this.extractError(error, 'Không thể đổi trạng thái áp dụng.'));
    } finally {
      this.saving = false;
    }
  }

  async deleteDeviceValue(item: DeviceRow): Promise<void> {
    if (!item.isLocal || !this.canDeleteDevice) return;
    const accepted = await this.confirmDialog.confirmDeactivate({
      message: `Ngừng áp dụng loại thiết bị riêng "${item.tenLoai}"?`,
      acceptLabel: 'Ngừng áp dụng',
      rejectLabel: 'Hủy',
    });
    if (!accepted) return;
    this.saving = true;
    try {
      await this.deviceApi.deleteUnitItem(item.id);
      await this.reloadDeviceTree();
      this.notificationService.show('success', 'Đã ngừng áp dụng loại thiết bị riêng.');
    } catch (error: unknown) {
      this.notificationService.show('error', this.extractError(error, 'Không thể ngừng áp dụng loại thiết bị.'));
    } finally {
      this.saving = false;
    }
  }

  private flattenDeviceTree(items: RefLoaiThietBiDto[], depth = 0): DeviceRow[] {
    return items.flatMap((item) => [
      { ...item, depth },
      ...this.flattenDeviceTree(item.children ?? [], depth + 1),
    ]);
  }

  private normalize(value: string): string {
    return value
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .toLowerCase()
      .trim();
  }

  private extractError(error: unknown, fallback: string): string {
    return (
      (error as { error?: { error?: { message?: string } } })?.error?.error?.message
      ?? fallback
    );
  }
}
