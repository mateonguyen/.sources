import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { TreeNode } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { DialogModule } from 'primeng/dialog';
import { DropdownModule } from 'primeng/dropdown';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { TooltipModule } from 'primeng/tooltip';
import { TreeTableModule } from 'primeng/treetable';
import { TreeTableNodeCollapseEvent, TreeTableNodeExpandEvent } from 'primeng/treetable';
import {
  RefLoaiThietBiApi,
  RefLoaiThietBiDto,
  UpsertRefLoaiThietBiRequest,
} from './ref-loai-thiet-bi.api';
import { ConfirmDialogWrapperService } from '../../shared/ui/confirm-dialog-wrapper.service';
import { EmptyStateComponent } from '../../shared/ui/empty-state.component';
import { LoadingOverlayComponent } from '../../shared/ui/loading-overlay.component';
import { SectionCardComponent } from '../../shared/ui/section-card.component';
import { NotificationService } from '../../core/ui/notification.service';

type DialogMode = 'create' | 'edit';

@Component({
  selector: 'app-ref-loai-thiet-bi-page',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    SectionCardComponent,
    EmptyStateComponent,
    LoadingOverlayComponent,
    InputTextModule,
    InputNumberModule,
    DropdownModule,
    CheckboxModule,
    ButtonModule,
    TooltipModule,
    TreeTableModule,
    DialogModule,
  ],
  templateUrl: './ref-loai-thiet-bi.page.html',
  styleUrl: './ref-loai-thiet-bi.page.scss',
})
export class RefLoaiThietBiPage {
  data: RefLoaiThietBiDto[] = [];
  treeNodes: TreeNode<RefLoaiThietBiDto>[] = [];
  loading = false;
  saving = false;

  // Giu trang thai dong/mo giua cac lan build lai cay (sau moi lan luu/xoa),
  // vi TreeNode duoc tao moi hoan toan tu data moi tra ve.
  private readonly expandedKeys = new Set<string>();

  dialogVisible = false;
  dialogMode: DialogMode = 'create';
  // Node cha ma node dang them/sua thuoc ve - null = cap goc (nhom).
  dialogParentId: number | null = null;
  dialogParentLabel = '';
  dialogEditingId: number | null = null;

  readonly statusOptions = [
    { label: 'Đang hoạt động', value: true },
    { label: 'Ngừng hoạt động', value: false },
  ];

  // Do rong 1 cap thut le, dung de ve duong noi cay (giong don-vi.page) -
  // khop voi viec da tat margin-left mac dinh cua p-treetable-toggler
  // (xem override trong styles/components/table.scss).
  readonly treeIndentStepPx = 22;

  guideColumns(level: number): number[] {
    return Array.from({ length: level }, (_, i) => i);
  }

  readonly form: FormGroup;

  constructor(
    private readonly api: RefLoaiThietBiApi,
    private readonly fb: FormBuilder,
    private readonly notificationService: NotificationService,
    private readonly confirmDialog: ConfirmDialogWrapperService,
  ) {
    this.form = this.fb.group({
      maLoai: ['', [Validators.required, Validators.maxLength(50)]],
      tenLoai: ['', [Validators.required, Validators.maxLength(200)]],
      laTongHop: [false],
      sortOrder: [0],
      isActive: [true],
    });
    this.load();
  }

  get dialogTitle(): string {
    if (this.dialogMode === 'edit') {
      return 'Sửa loại thiết bị';
    }
    return this.dialogParentId === null ? 'Thêm nhóm gốc' : `Thêm loại con của "${this.dialogParentLabel}"`;
  }

  async load(): Promise<void> {
    this.loading = true;
    try {
      this.data = await this.api.getAdminTree();
      this.treeNodes = this.buildTreeNodes(this.data, true);
    } catch (error: unknown) {
      this.notificationService.show('error', this.extractErrorMessage(error, 'Không thể tải dữ liệu loại thiết bị.'));
      this.data = [];
      this.treeNodes = [];
    } finally {
      this.loading = false;
    }
  }

  private buildTreeNodes(items: RefLoaiThietBiDto[], isRoot: boolean): TreeNode<RefLoaiThietBiDto>[] {
    return items.map((item) => {
      const key = String(item.id);
      return {
        key,
        label: item.tenLoai,
        data: item,
        expanded: this.expandedKeys.has(key) || isRoot,
        children: item.children.length > 0 ? this.buildTreeNodes(item.children, false) : [],
      };
    });
  }

  onNodeExpand(event: TreeTableNodeExpandEvent): void {
    if (event.node.key) {
      this.expandedKeys.add(event.node.key);
    }
  }

  onNodeCollapse(event: TreeTableNodeCollapseEvent): void {
    if (event.node.key) {
      this.expandedKeys.delete(event.node.key);
    }
  }

  openCreateRootDialog(): void {
    this.dialogMode = 'create';
    this.dialogParentId = null;
    this.dialogParentLabel = '';
    this.dialogEditingId = null;
    this.form.reset({
      maLoai: '',
      tenLoai: '',
      laTongHop: false,
      sortOrder: this.data.length,
      isActive: true,
    });
    this.dialogVisible = true;
  }

  openCreateChildDialog(parent: RefLoaiThietBiDto): void {
    this.dialogMode = 'create';
    this.dialogParentId = parent.id;
    this.dialogParentLabel = parent.tenLoai;
    this.dialogEditingId = null;
    this.form.reset({
      maLoai: '',
      tenLoai: '',
      laTongHop: false,
      sortOrder: parent.children.length,
      isActive: true,
    });
    this.dialogVisible = true;
  }

  openEditDialog(node: RefLoaiThietBiDto): void {
    this.dialogMode = 'edit';
    this.dialogParentId = node.parentId;
    this.dialogParentLabel = '';
    this.dialogEditingId = node.id;
    this.form.reset({
      maLoai: node.maLoai,
      tenLoai: node.tenLoai,
      laTongHop: node.laTongHop,
      sortOrder: node.sortOrder,
      isActive: node.isActive,
    });
    this.dialogVisible = true;
  }

  closeDialog(): void {
    this.dialogVisible = false;
  }

  isInvalid(controlName: string): boolean {
    const control = this.form.get(controlName);
    return !!control && control.invalid && (control.dirty || control.touched);
  }

  async submit(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving = true;
    try {
      const value = this.form.getRawValue();
      const request: UpsertRefLoaiThietBiRequest = {
        parentId: this.dialogParentId,
        maLoai: value.maLoai.trim(),
        tenLoai: value.tenLoai.trim(),
        laTongHop: !!value.laTongHop,
        sortOrder: value.sortOrder ?? 0,
        isActive: value.isActive,
      };

      if (this.dialogMode === 'edit' && this.dialogEditingId) {
        await this.api.update(this.dialogEditingId, request);
        this.notificationService.show('success', 'Cập nhật loại thiết bị thành công.');
      } else {
        const created = await this.api.create(request);
        if (created.parentId !== null) {
          this.expandedKeys.add(String(created.parentId));
        }
        this.notificationService.show('success', 'Thêm loại thiết bị thành công.');
      }

      this.dialogVisible = false;
      await this.load();
    } catch (error: unknown) {
      this.notificationService.show('error', this.extractErrorMessage(error, 'Không thể lưu loại thiết bị.'));
    } finally {
      this.saving = false;
    }
  }

  isFirstSibling(node: RefLoaiThietBiDto): boolean {
    const siblings = this.findSiblings(node);
    return siblings.length === 0 || siblings[0].id === node.id;
  }

  isLastSibling(node: RefLoaiThietBiDto): boolean {
    const siblings = this.findSiblings(node);
    return siblings.length === 0 || siblings[siblings.length - 1].id === node.id;
  }

  async moveNodeUp(node: RefLoaiThietBiDto): Promise<void> {
    await this.swapSortOrder(node, -1);
  }

  async moveNodeDown(node: RefLoaiThietBiDto): Promise<void> {
    await this.swapSortOrder(node, 1);
  }

  private async swapSortOrder(node: RefLoaiThietBiDto, direction: -1 | 1): Promise<void> {
    if (this.saving) {
      return;
    }

    const siblings = this.findSiblings(node);
    const index = siblings.findIndex((x) => x.id === node.id);
    const neighborIndex = index + direction;
    if (index === -1 || neighborIndex < 0 || neighborIndex >= siblings.length) {
      return;
    }

    const neighbor = siblings[neighborIndex];
    this.saving = true;
    try {
      await this.api.update(node.id, this.buildRequestFrom(node, neighbor.sortOrder));
      await this.api.update(neighbor.id, this.buildRequestFrom(neighbor, node.sortOrder));
      await this.load();
    } catch (error: unknown) {
      this.notificationService.show('error', this.extractErrorMessage(error, 'Không thể đổi thứ tự.'));
    } finally {
      this.saving = false;
    }
  }

  private buildRequestFrom(item: RefLoaiThietBiDto, sortOrder: number): UpsertRefLoaiThietBiRequest {
    return {
      parentId: item.parentId,
      maLoai: item.maLoai,
      tenLoai: item.tenLoai,
      laTongHop: item.laTongHop,
      sortOrder,
      isActive: item.isActive,
    };
  }

  // Tim danh sach "anh em" (cung cha) cua node trong cay hien tai - dung de
  // xac dinh vi tri dau/cuoi va hoan vi sortOrder khi bam mui ten.
  private findSiblings(node: RefLoaiThietBiDto): RefLoaiThietBiDto[] {
    if (node.parentId === null) {
      return [...this.data].sort((a, b) => a.sortOrder - b.sortOrder);
    }

    const parent = this.findNodeById(this.data, node.parentId);
    return parent ? [...parent.children].sort((a, b) => a.sortOrder - b.sortOrder) : [];
  }

  private findNodeById(items: RefLoaiThietBiDto[], id: number): RefLoaiThietBiDto | null {
    for (const item of items) {
      if (item.id === id) {
        return item;
      }
      const found = this.findNodeById(item.children, id);
      if (found) {
        return found;
      }
    }
    return null;
  }

  async deleteNode(node: RefLoaiThietBiDto): Promise<void> {
    const confirmed = await this.confirmDialog.confirmDeactivate({
      message: `Xác nhận vô hiệu hóa "${node.tenLoai}"?`,
      acceptLabel: 'Vô hiệu hóa',
      rejectLabel: 'Hủy',
    });
    if (!confirmed) {
      return;
    }

    try {
      await this.api.delete(node.id);
      this.notificationService.show('success', 'Vô hiệu hóa thành công.');
      await this.load();
    } catch (error: unknown) {
      this.notificationService.show('error', this.extractErrorMessage(error, 'Không thể vô hiệu hóa.'));
    }
  }

  private extractErrorMessage(error: unknown, fallback: string): string {
    return (
      (error as { error?: { error?: { message?: string } } })?.error?.error?.message ?? fallback
    );
  }
}
