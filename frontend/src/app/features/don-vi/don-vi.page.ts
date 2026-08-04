import { CommonModule } from '@angular/common';
import { Component, ElementRef, ViewChild } from '@angular/core';
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
  /** Ten da highlight tu khoa, tinh san 1 lan khi build rows - tranh goi lai
   * regex/innerHTML moi vong change-detection (nguyen nhan giat khi go tim). */
  label: string;
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
  @ViewChild('treeBody') treeBodyRef?: ElementRef<HTMLDivElement>;

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
  khoiDonViOptions: SelectOption[] = [];
  readonly cheDoNhapLieuOptions: SelectOption[] = [
    { label: 'Tự nhập', value: 'TU_NHAP' },
    { label: 'Tổng hợp', value: 'TONG_HOP' },
  ];

  private readonly expandedIds = new Set<number>();
  private readonly parentMap = new Map<number, number | null>();
  private filterDebounceHandle: ReturnType<typeof setTimeout> | null = null;

  readonly form = this.formBuilder.group({
    maDonVi: ['', [Validators.required, Validators.maxLength(50)]],
    tenDonVi: ['', [Validators.required, Validators.maxLength(200)]],
    tenVietTat: [''],
    parentNode: [null as TreeNode<ParentTreeNodeData> | null],
    diaChi: [''],
    capDonVi: [null as string | null],
    khoiDonVi: [null as string | null],
    cheDoNhapLieu: ['TU_NHAP'],
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
    this.form.controls.capDonVi.valueChanges.subscribe((value) => {
      if (!this.isTongHopCapDonVi(value)) {
        this.form.controls.cheDoNhapLieu.setValue('TU_NHAP', {
          emitEvent: false,
        });
      }
    });

    void this.initialize();
  }

  async initialize(): Promise<void> {
    this.loading = true;
    this.apiError = '';
    try {
      const [capDonViCode, khoiDonViCode, tree] = await Promise.all([
        this.codesApi.getByCode('CAP_DON_VI'),
        this.codesApi.getByCode('KHOI_DON_VI'),
        this.donViApi.getTree(),
      ]);

      this.capDonViOptions = capDonViCode.values.map((item: CodeValueDto) => ({
        label: item.name,
        value: item.value,
      }));
      this.khoiDonViOptions = khoiDonViCode.values.map((item: CodeValueDto) => ({
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
      khoiDonVi: detail.khoiDonVi ?? null,
      cheDoNhapLieu: detail.cheDoNhapLieu ?? 'TU_NHAP',
      websiteNoiBo: detail.websiteNoiBo ?? '',
      websiteInternet: detail.websiteInternet ?? '',
      tongBienChe: detail.tongBienChe ?? null,
      isActive: this.selected.isActive,
    });
    this.scrollNodeIntoView(id);
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
      khoiDonVi: null,
      cheDoNhapLieu: 'TU_NHAP',
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
      khoiDonVi: null,
      cheDoNhapLieu: 'TU_NHAP',
      websiteNoiBo: '',
      websiteInternet: '',
      tongBienChe: null,
      isActive: true,
    });
    this.scrollNodeIntoView(node.id);
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
        khoiDonVi: value.khoiDonVi ?? undefined,
        cheDoNhapLieu: this.isTongHopCapDonVi(value.capDonVi)
          ? (value.cheDoNhapLieu ?? 'TU_NHAP')
          : 'TU_NHAP',
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
      message: `Xác nhận xóa đơn vị ${this.selected.tenDonVi}?`,
      acceptLabel: 'Xóa',
      rejectLabel: 'Hủy',
      countdownSeconds: 5,
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
    // Cap nhat gia tri o nhap ngay (go phim muot), nhung debounce phan build
    // lai cay (5000+ node) + highlight de tranh giat khi go nhanh.
    this.treeFilter = value;
    if (this.filterDebounceHandle !== null) {
      clearTimeout(this.filterDebounceHandle);
    }
    this.filterDebounceHandle = setTimeout(() => {
      this.filterDebounceHandle = null;
      this.rebuildTreeRows();
    }, 220);
  }

  clearTreeFilter(): void {
    if (this.filterDebounceHandle !== null) {
      clearTimeout(this.filterDebounceHandle);
      this.filterDebounceHandle = null;
    }
    this.treeFilter = '';
    this.rebuildTreeRows();
    // Sau khi xoa tim kiem, cuon ve dung vi tri don vi dang chon (neu co) de
    // nguoi dung khong bi "mat dau" don vi vua tim thay trong cay day du.
    if (this.selected) {
      this.scrollNodeIntoView(this.selected.id);
    } else if (this.selectedParent) {
      this.scrollNodeIntoView(this.selectedParent.id);
    }
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

  resolveKhoiDonViLabel(value: string | null | undefined): string {
    if (!value) {
      return 'Chưa phân loại';
    }

    return (
      this.khoiDonViOptions.find((item) => item.value === value)?.label ??
      value
    );
  }

  get showCheDoNhapLieuField(): boolean {
    return this.isTongHopCapDonVi(this.form.controls.capDonVi.value);
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

  /** Cuon panel cay don vi de dua node dang chon vao vung nhin, sau khi DOM
   * da cap nhat theo treeRows moi (setTimeout de doi Angular render xong). */
  private scrollNodeIntoView(id: number): void {
    setTimeout(() => {
      const container = this.treeBodyRef?.nativeElement;
      if (!container) {
        return;
      }
      const el = container.querySelector<HTMLElement>(`[data-node-id="${id}"]`);
      el?.scrollIntoView({ block: 'center', behavior: 'smooth' });
    });
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
    forceInclude = false,
  ): DonViTreeRow[] {
    const rows: DonViTreeRow[] = [];

    for (let index = 0; index < items.length; index += 1) {
      const item = items[index];
      const selfTextMatches = !!keyword && this.matches(item, keyword);
      // Neu chinh don vi nay khop tu khoa (hoac mot to tien da khop), hien
      // thi TOAN BO cay con cua no khong loc tiep theo tu khoa - nguoi dung
      // tim thay don vi cha van xem/duyet duoc het cac don vi con ben trong.
      const childForceInclude = forceInclude || selfTextMatches;
      const childRows = this.buildTreeRows(
        item.children ?? [],
        keyword,
        ignoreExpansion,
        level + 1,
        childForceInclude,
      );
      const includeSelf = !keyword || forceInclude || selfTextMatches;
      const hasVisibleChildren = childRows.length > 0;

      if (keyword && !includeSelf && !hasVisibleChildren) {
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
        matched: selfTextMatches,
        label: this.highlightNodeName(item.tenDonVi),
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

  private isTongHopCapDonVi(capDonVi: string | null | undefined): boolean {
    return capDonVi === 'CAP_1';
  }
}
