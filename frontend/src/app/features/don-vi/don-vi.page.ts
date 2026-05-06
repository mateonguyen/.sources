import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import {
  FormBuilder,
  FormsModule,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { TreeNode } from 'primeng/api';
import { DropdownModule } from 'primeng/dropdown';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { TreeSelectModule } from 'primeng/treeselect';
import { DonViApi, DonViDto, UpsertDonViRequest } from './don-vi.api';
import { CodeValueDto, CodesApi } from '../codes/codes.api';
import { ConfirmDialogWrapperService } from '../../shared/ui/confirm-dialog-wrapper.service';
import { EmptyStateComponent } from '../../shared/ui/empty-state.component';
import { FormActionBarComponent } from '../../shared/ui/form-action-bar.component';
import { LoadingOverlayComponent } from '../../shared/ui/loading-overlay.component';
import { SectionCardComponent } from '../../shared/ui/section-card.component';
import { NotificationService } from '../../core/ui/notification.service';

interface ParentTreeNodeData {
  id: number;
  tenDonVi: string;
}

interface DonViTreeRow {
  item: DonViDto;
  level: number;
  guideColumns: number[];
  expandable: boolean;
  expanded: boolean;
  matched: boolean;
}

interface SelectOption {
  label: string;
  value: string;
}

type EditorMode = 'create-root' | 'create-child' | 'edit';

@Component({
  selector: 'app-don-vi-page',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    SectionCardComponent,
    EmptyStateComponent,
    FormActionBarComponent,
    LoadingOverlayComponent,
    InputTextModule,
    DropdownModule,
    InputNumberModule,
    TreeSelectModule,
    CheckboxModule,
    ButtonModule,
  ],
  templateUrl: './don-vi.page.html',
  styleUrl: './don-vi.page.scss',
})
export class DonViPage {
  readonly treeIndentStepPx = 30;

  data: DonViDto[] = [];
  treeRows: DonViTreeRow[] = [];
  selected: DonViDto | null = null;
  selectedParent: DonViDto | null = null;
  treeFilter = '';
  loading = false;
  saving = false;
  apiError = '';
  editorMode: EditorMode = 'create-root';
  parentTreeOptions: TreeNode<ParentTreeNodeData>[] = [];
  capDonViOptions: SelectOption[] = [];

  private readonly expandedIds = new Set<number>();
  private readonly parentMap = new Map<number, number | null>();

  readonly form = this.formBuilder.group({
    maDonVi: ['', [Validators.required, Validators.maxLength(50)]],
    tenDonVi: ['', [Validators.required, Validators.maxLength(200)]],
    tenVietTat: [''],
    parentNode: [null as TreeNode<ParentTreeNodeData> | null],
    diaChi: [''],
    capDonVi: [null as string | null],
    websiteNoiBo: [''],
    websiteInternet: [''],
    tongBienChe: [null as number | null],
    isActive: [true],
  });

  constructor(
    private readonly donViApi: DonViApi,
    private readonly codesApi: CodesApi,
    private readonly formBuilder: FormBuilder,
    private readonly notificationService: NotificationService,
    private readonly confirmDialog: ConfirmDialogWrapperService,
  ) {
    void this.initialize();
  }

  async initialize(): Promise<void> {
    this.loading = true;
    this.apiError = '';
    try {
      const [capDonViCode, tree] = await Promise.all([
        this.codesApi.getByCode('CAP_DON_VI'),
        this.donViApi.getTree(),
      ]);

      this.capDonViOptions = capDonViCode.values.map((item: CodeValueDto) => ({
        label: item.name,
        value: item.value,
      }));
      this.applyTree(tree);
    } finally {
      this.loading = false;
    }
  }

  async load(): Promise<void> {
    this.loading = true;
    this.apiError = '';
    try {
      const tree = await this.donViApi.getTree();
      this.applyTree(tree);
    } finally {
      this.loading = false;
    }
  }

  async select(id: number): Promise<void> {
    const detail = await this.donViApi.getById(id);

    this.selected = detail;
    this.selectedParent = null;
    this.editorMode = 'edit';
    this.expandSelectionPath(id);
    this.rebuildParentTreeOptions();
    this.rebuildTreeRows();
    this.form.patchValue({
      maDonVi: this.selected.maDonVi,
      tenDonVi: this.selected.tenDonVi,
      tenVietTat: this.selected.tenVietTat ?? '',
      parentNode: this.findParentNodeById(this.selected.parentId ?? null),
      diaChi: detail.diaChi ?? '',
      capDonVi: detail.capDonVi ?? null,
      websiteNoiBo: detail.websiteNoiBo ?? '',
      websiteInternet: detail.websiteInternet ?? '',
      tongBienChe: detail.tongBienChe ?? null,
      isActive: this.selected.isActive,
    });
  }

  createNew(): void {
    this.selected = null;
    this.selectedParent = null;
    this.editorMode = 'create-root';
    this.apiError = '';
    this.rebuildParentTreeOptions();
    this.form.reset({
      maDonVi: '',
      tenDonVi: '',
      tenVietTat: '',
      parentNode: null,
      diaChi: '',
      capDonVi: null,
      websiteNoiBo: '',
      websiteInternet: '',
      tongBienChe: null,
      isActive: true,
    });
  }

  addChildFromNode(node: DonViDto): void {
    this.selected = null;
    this.selectedParent = node;
    this.editorMode = 'create-child';
    this.apiError = '';
    this.expandSelectionPath(node.id);
    this.rebuildParentTreeOptions();
    this.rebuildTreeRows();
    this.form.reset({
      maDonVi: '',
      tenDonVi: '',
      tenVietTat: '',
      parentNode: this.findParentNodeById(node.id),
      diaChi: '',
      capDonVi: null,
      websiteNoiBo: '',
      websiteInternet: '',
      tongBienChe: null,
      isActive: true,
    });
  }

  async save(): Promise<void> {
    if (this.form.invalid || this.saving) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving = true;
    this.apiError = '';
    try {
      const value = this.form.getRawValue();
      const payload: UpsertDonViRequest = {
        maDonVi: value.maDonVi ?? '',
        tenDonVi: value.tenDonVi ?? '',
        tenVietTat: value.tenVietTat ?? undefined,
        parentId: value.parentNode?.data?.id,
        diaChi: value.diaChi ?? undefined,
        capDonVi: value.capDonVi ?? undefined,
        websiteNoiBo: value.websiteNoiBo ?? undefined,
        websiteInternet: value.websiteInternet ?? undefined,
        tongBienChe: value.tongBienChe ?? undefined,
        isActive: !!value.isActive,
      };

      if (this.selected) {
        await this.donViApi.update(this.selected.id, payload);
        this.notificationService.show('success', 'Cập nhật đơn vị thành công.');
      } else {
        await this.donViApi.create(payload);
        this.notificationService.show('success', 'Tạo đơn vị thành công.');
      }

      await this.load();
      this.createNew();
    } catch (error: unknown) {
      this.apiError =
        (error as { error?: { error?: { message?: string } } })?.error?.error
          ?.message ?? 'Không thể lưu đơn vị.';
    } finally {
      this.saving = false;
    }
  }

  async softDelete(): Promise<void> {
    if (!this.selected) {
      return;
    }

    const confirmed = await this.confirmDialog.confirmDelete({
      message: `Xác nhận xóa mềm đơn vị ${this.selected.tenDonVi}?`,
      acceptLabel: 'Xóa mềm',
      rejectLabel: 'Hủy',
    });
    if (!confirmed) {
      return;
    }

    await this.donViApi.delete(this.selected.id);
    this.notificationService.show('success', 'Xóa đơn vị thành công.');
    this.createNew();
    await this.load();
  }

  onTreeFilterChange(value: string): void {
    this.treeFilter = value;
    this.rebuildTreeRows();
  }

  clearTreeFilter(): void {
    this.onTreeFilterChange('');
  }

  toggleExpanded(id: number): void {
    if (this.expandedIds.has(id)) {
      this.expandedIds.delete(id);
    } else {
      this.expandedIds.add(id);
    }

    this.rebuildTreeRows();
  }

  trackByRow(_: number, row: DonViTreeRow): number {
    return row.item.id;
  }

  resolveCapDonViLabel(value: string | null | undefined): string {
    if (!value) {
      return 'Chưa phân loại';
    }

    return (
      this.capDonViOptions.find((item) => item.value === value)?.label ?? value
    );
  }

  treeIndentPx(level: number): number {
    return level * this.treeIndentStepPx;
  }

  highlightNodeName(name: string): string {
    const searchValue = this.treeFilter.trim();
    if (!searchValue) {
      return this.escapeHtml(name);
    }

    const pattern = new RegExp(this.escapeRegExp(searchValue), 'ig');
    if (!pattern.test(name)) {
      return this.escapeHtml(name);
    }

    return name.replace(pattern, (match) => {
      return `<mark class="node-name-mark">${this.escapeHtml(match)}</mark>`;
    });
  }

  private rebuildParentTreeOptions(): void {
    const excludedId =
      this.editorMode === 'edit' ? (this.selected?.id ?? null) : null;
    this.parentTreeOptions = this.buildParentTreeNodes(this.data, excludedId);
  }

  private applyTree(tree: DonViDto[]): void {
    this.data = tree;
    this.reindexTree();
    this.expandRootNodes();
    this.rebuildParentTreeOptions();
    this.rebuildTreeRows();
  }

  private buildParentTreeNodes(
    items: DonViDto[],
    excludedRootId: number | null,
  ): TreeNode<ParentTreeNodeData>[] {
    const nodes: TreeNode<ParentTreeNodeData>[] = [];

    for (const item of items) {
      if (excludedRootId === item.id) {
        continue;
      }

      const children = this.buildParentTreeNodes(
        item.children ?? [],
        excludedRootId,
      );
      nodes.push({
        key: String(item.id),
        label: item.tenDonVi,
        data: {
          id: item.id,
          tenDonVi: item.tenDonVi,
        },
        selectable: true,
        expanded: true,
        children,
        styleClass: item.isActive ? undefined : 'is-inactive',
      });
    }

    return nodes;
  }

  private findParentNodeById(
    id: number | null,
    nodes: TreeNode<ParentTreeNodeData>[] = this.parentTreeOptions,
  ): TreeNode<ParentTreeNodeData> | null {
    if (typeof id !== 'number') {
      return null;
    }

    for (const node of nodes) {
      if (node.data?.id === id) {
        return node;
      }

      const childMatch = this.findParentNodeById(id, node.children ?? []);
      if (childMatch) {
        return childMatch;
      }
    }

    return null;
  }

  private expandRootNodes(): void {
    if (this.expandedIds.size > 0) {
      return;
    }

    for (const root of this.data) {
      this.expandedIds.add(root.id);
    }
  }

  private expandSelectionPath(id: number): void {
    this.expandedIds.add(id);

    let current = this.parentMap.get(id) ?? null;
    while (typeof current === 'number') {
      this.expandedIds.add(current);
      current = this.parentMap.get(current) ?? null;
    }
  }

  private reindexTree(): void {
    this.parentMap.clear();

    const visit = (items: DonViDto[], parentId: number | null): void => {
      for (const item of items) {
        this.parentMap.set(item.id, parentId);
        visit(item.children ?? [], item.id);
      }
    };

    visit(this.data, null);
  }

  private rebuildTreeRows(): void {
    this.treeRows = this.buildTreeRows(
      this.data,
      this.normalize(this.treeFilter),
      false,
    );
  }

  private buildTreeRows(
    items: DonViDto[],
    keyword: string,
    ignoreExpansion: boolean,
    level = 0,
  ): DonViTreeRow[] {
    const rows: DonViTreeRow[] = [];

    for (let index = 0; index < items.length; index += 1) {
      const item = items[index];
      const childRows = this.buildTreeRows(
        item.children ?? [],
        keyword,
        ignoreExpansion,
        level + 1,
      );
      const selfMatches = !keyword || this.matches(item, keyword);
      const hasVisibleChildren = childRows.length > 0;

      if (keyword && !selfMatches && !hasVisibleChildren) {
        continue;
      }

      const expanded =
        ignoreExpansion || !!keyword || this.expandedIds.has(item.id);
      rows.push({
        item,
        level,
        guideColumns: Array.from({ length: level }, (_, guide) => guide),
        expandable: (item.children?.length ?? 0) > 0,
        expanded,
        matched: !!keyword && this.matches(item, keyword),
      });

      if (expanded || ignoreExpansion || !!keyword) {
        rows.push(...childRows);
      }
    }

    return rows;
  }

  private matches(item: DonViDto, keyword: string): boolean {
    const searchText = [item.maDonVi, item.tenDonVi, item.tenVietTat]
      .filter(Boolean)
      .join(' ');

    return this.normalize(searchText).includes(keyword);
  }

  private escapeRegExp(value: string): string {
    return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  }

  private escapeHtml(value: string): string {
    return value
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#39;');
  }

  private normalize(value: string | null | undefined): string {
    return (value ?? '').trim().toLowerCase();
  }
}
