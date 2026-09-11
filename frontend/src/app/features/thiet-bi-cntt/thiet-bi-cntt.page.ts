import { CommonModule } from '@angular/common';
import {
  Component,
  computed,
  signal,
  WritableSignal,
} from '@angular/core';
import {
  AbstractControl,
  FormBuilder,
  FormsModule,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DropdownModule } from 'primeng/dropdown';
import { AutoCompleteModule } from 'primeng/autocomplete';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { InputTextareaModule } from 'primeng/inputtextarea';
import { TooltipModule } from 'primeng/tooltip';
import { DialogModule } from 'primeng/dialog';
import { TreeSelectModule } from 'primeng/treeselect';
import { TreeNode } from 'primeng/api';
import { AuthService } from '../../core/auth/auth.service';
import { NotificationService } from '../../core/ui/notification.service';
import { DonViApi, DonViDto } from '../don-vi/don-vi.api';
import { ConfirmDialogWrapperService } from '../../shared/ui/confirm-dialog-wrapper.service';
import { EmptyStateComponent } from '../../shared/ui/empty-state.component';
import { FilterBarComponent } from '../../shared/ui/filter-bar.component';
import { LoadingOverlayComponent } from '../../shared/ui/loading-overlay.component';
import { SectionCardComponent } from '../../shared/ui/section-card.component';
import { TongHopModeBannerComponent } from '../../shared/ui/tong-hop-mode-banner.component';
import {
  HeThongThongTinOptionDto,
  RefLoaiThietBiDto,
  ThietBiCatalogDto,
  ThietBiCnttApi,
  ThietBiCnttDto,
  UpsertThietBiCnttRequest,
} from './thiet-bi-cntt.api';

interface SelectOption<TValue extends string | number | null> {
  label: string;
  value: TValue;
}

interface LoaiThietBiOption {
  label: string;
  // Bản không dấu, riêng "đ"/"Đ" quy về "d"/"D" — PrimeNG tự bỏ dấu kiểu
  // NFKD trước khi lọc, nhưng "đ" là ký tự Unicode độc lập nên NFKD không
  // tách được, khiến gõ "de ban" không khớp "để bàn". Field này bù cho lỗ
  // hổng đó, dùng làm filterBy phụ trên p-dropdown.
  searchLabel: string;
  value: number;
  laTongHop: boolean;
}

interface ThietBiCategoryView {
  id: number;
  key: string;
  ordinal: number;
  label: string;
  maLoai: string;
  laTongHop: boolean;
  items: ThietBiCnttDto[];
  recordCount: number;
  currentCount: number;
  brokenCount: number;
  primaryItem: ThietBiCnttDto | null;
}

interface ThietBiGroupView {
  id: number;
  key: string;
  roman: string;
  label: string;
  categories: ThietBiCategoryView[];
  recordCount: number;
  currentCount: number;
  configuredCount: number;
}

interface AggregateDraft {
  soLuongHienDung: number;
  tinhTrang: string;
  ghiChu: string;
}

@Component({
  selector: 'app-thiet-bi-cntt-page',
  standalone: true,
  imports: [
    TongHopModeBannerComponent,
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    SectionCardComponent,
    FilterBarComponent,
    EmptyStateComponent,
    LoadingOverlayComponent,
    AutoCompleteModule,
    DropdownModule,
    TreeSelectModule,
    InputNumberModule,
    InputTextModule,
    InputTextareaModule,
    ButtonModule,
    TooltipModule,
    DialogModule,
  ],
  templateUrl: './thiet-bi-cntt.page.html',
  styleUrl: './thiet-bi-cntt.page.scss',
})
export class ThietBiCnttPage {
  // Không dùng `overflow-hidden` của panel dùng chung: dropdown có ô cuộn
  // danh sách riêng, lớp đó có thể làm phần option bị cắt khi panel nằm trong
  // card. Overlay được append ra body và giữ class riêng để hiển thị ổn định.
  readonly selectPanelStyleClass =
    'device-select-panel rounded-lg border border-[var(--app-border)] bg-[var(--app-surface)] shadow-panel';

  private static soLuongValidator(
    control: AbstractControl,
  ): ValidationErrors | null {
    const tong = Number(control.get('soLuongTong')?.value ?? 0);
    const hienDung = Number(control.get('soLuongHienDung')?.value ?? 0);
    const hong = Number(control.get('soLuongHong')?.value ?? 0);
    return hienDung + hong > tong ? { soLuongVuotQua: true } : null;
  }

  readonly form = this.formBuilder.group(
    {
      loaiThietBiId: [null as number | null, [Validators.required]],
      tenThietBi: [''],
      hangSanXuat: [''],
      model: [''],
      cauHinh: [''],
      heDieuHanh: [''],
      donViSuDung: [''],
      soLuongTong: [0],
      soLuongHienDung: [0, [Validators.required]],
      soLuongHong: [0],
      tinhTrang: [''],
      ghiChu: [''],
      ungDungIds: [[] as number[]],
    },
    { validators: [ThietBiCnttPage.soLuongValidator] },
  );

  readonly donViId = computed(() => this.authService.profile()?.donViId ?? 0);
  readonly selectedLoaiId = signal<number | null>(null);

  readonly selectedLoaiThietBi = computed(() => {
    const loaiThietBiId = this.selectedLoaiId();
    return (
      this.loaiThietBiOptions().find((item) => item.value === loaiThietBiId) ??
      null
    );
  });

  readonly isTongHop = computed(
    () => this.selectedLoaiThietBi()?.laTongHop ?? false,
  );

  // Node cua p-treeSelect tuong ung voi loaiThietBiId hien tai trong form -
  // re-derive tu form control (khong phai formControlName truc tiep tren
  // p-treeSelect) de giu nguyen kieu du lieu number|null cho loaiThietBiId
  // o moi noi khac trong file (rat nhieu cho dang gia dinh dieu nay).
  readonly loaiThietBiSelectedNode = computed(() => {
    const id = this.selectedLoaiId();
    if (id === null) {
      return null;
    }
    return this.findLoaiThietBiTreeNode(this.loaiThietBiTreeOptions(), id);
  });

  items = signal<ThietBiCnttDto[]>([]);
  loading = signal(false);
  saving = signal(false);
  formDialogVisible = signal(false);
  selectedId = signal<number | null>(null);
  loaiThietBiOptions = signal<LoaiThietBiOption[]>([]);
  loaiThietBiTree = signal<RefLoaiThietBiDto[]>([]);
  loaiThietBiTreeOptions = signal<TreeNode<{ id: number; laTongHop: boolean }>[]>([]);
  ungDungSearchQuery = signal('');
  ungDungSuggestions = signal<string[]>([]);
  // Guong lai gia tri cua form.controls.ungDungIds duoi dang signal - vi
  // computed() chi theo doi thay doi cua signal, doc truc tiep .value cua
  // FormControl (reactive forms, khong phai signal) se khong bao gio kich
  // hoat selectedUngDungItems tinh lai duoc.
  ungDungIdsSignal = signal<number[]>([]);
  heThongThongTin = signal<HeThongThongTinOptionDto[]>([]);
  donViSuDungTree = signal<DonViDto[]>([]);
  hangSanXuatCatalog = signal<string[]>([]);
  heDieuHanhCatalog = signal<string[]>([]);
  modelCatalogByHang = signal<Record<string, string[]>>({});
  modelCatalogGlobal = signal<string[]>([]);
  modelSuggestions = signal<string[]>([]);
  hangSanXuatSuggestions = signal<string[]>([]);
  heDieuHanhSuggestions = signal<string[]>([]);
  expandedGroups = signal<Record<string, boolean>>({});
  expandedRows = signal<Record<string, boolean>>({});
  aggregateDrafts = signal<Record<number, AggregateDraft>>({});
  savingAggregateId = signal<number | null>(null);

  filterLoaiThietBiId = signal<number | null>(null);
  filterTenThietBi = signal<string>('');

  readonly deviceGroups = computed<ThietBiGroupView[]>(() => {
    const roots = this.sortLoaiNodes(this.loaiThietBiTree());
    const allItems = this.items();
    const selectedLoaiId = this.filterLoaiThietBiId();
    const query = this.normalizeSearch(this.filterTenThietBi());
    let ordinal = 0;

    return roots
      .map<ThietBiGroupView | null>((root, rootIndex) => {
        const rootMatches = this.matchesText(root.tenLoai, query);
        const leafNodes =
          root.children.length > 0 ? this.collectLeafNodes(root.children) : [root];
        const categories = leafNodes
          .map<ThietBiCategoryView | null>((leaf) => {
            ordinal += 1;
            const categoryItems = allItems.filter(
              (item) => item.loaiThietBiId === leaf.id,
            );
            const categoryMatches = this.matchesText(
              `${leaf.tenLoai} ${leaf.maLoai}`,
              query,
            );
            const matchingItems = query
              ? categoryItems.filter((item) =>
                  this.matchesDeviceItem(item, query),
                )
              : categoryItems;
            const matchesSelected =
              selectedLoaiId === null || selectedLoaiId === leaf.id;
            const matchesQuery =
              !query || rootMatches || categoryMatches || matchingItems.length > 0;

            if (!matchesSelected || !matchesQuery) {
              return null;
            }

            const visibleItems =
              query && !rootMatches && !categoryMatches
                ? matchingItems
                : categoryItems;

            return {
              id: leaf.id,
              key: `category-${leaf.id}`,
              ordinal,
              label: this.cleanCatalogLabel(leaf.tenLoai),
              maLoai: leaf.maLoai,
              laTongHop: leaf.laTongHop,
              items: visibleItems,
              recordCount: visibleItems.length,
              currentCount: visibleItems.reduce(
                (sum, item) => sum + item.soLuongHienDung,
                0,
              ),
              brokenCount: visibleItems.reduce(
                (sum, item) => sum + item.soLuongHong,
                0,
              ),
              primaryItem: visibleItems[0] ?? null,
            } satisfies ThietBiCategoryView;
          })
          .filter(
            (category): category is ThietBiCategoryView => category !== null,
          );

        if (categories.length === 0) {
          return null;
        }

        return {
          id: root.id,
          key: `group-${root.id}`,
          roman: this.toRoman(rootIndex + 1),
          label: this.cleanCatalogLabel(root.tenLoai),
          categories,
          recordCount: categories.reduce(
            (sum, category) => sum + category.recordCount,
            0,
          ),
          currentCount: categories.reduce(
            (sum, category) => sum + category.currentCount,
            0,
          ),
          configuredCount: categories.filter(
            (category) => category.recordCount > 0,
          ).length,
        } satisfies ThietBiGroupView;
      })
      .filter((group): group is ThietBiGroupView => group !== null);
  });

  readonly summary = computed(() => {
    const rows = this.items();
    return {
      currentCount: rows.reduce(
        (sum, item) => sum + item.soLuongHienDung,
        0,
      ),
      configuredTypeCount: new Set(rows.map((item) => item.loaiThietBiId)).size,
      totalTypeCount: this.loaiThietBiOptions().length,
      detailCount: rows.filter(
        (item) => !this.isLoaiTongHop(item.loaiThietBiId),
      ).length,
      brokenCount: rows.reduce((sum, item) => sum + item.soLuongHong, 0),
    };
  });

  readonly hasActiveFilters = computed(
    () =>
      this.filterLoaiThietBiId() !== null ||
      this.filterTenThietBi().trim().length > 0,
  );

  readonly loaiThietBiFilterOptions = computed<
    Array<SelectOption<number | null>>
  >(() => [
    { label: 'Tất cả loại thiết bị', value: null },
    ...this.loaiThietBiOptions().map((o) => ({
      label: o.label,
      value: o.value as number | null,
    })),
  ]);

  readonly heThongOptions = computed<Array<SelectOption<number>>>(() => {
    const donViId = this.donViId();
    return this.heThongThongTin()
      .filter((item) => item.donViId === donViId)
      .map((item) => ({
        label: item.maPhanMem
          ? `${item.tenPhanMem} (${item.maPhanMem})`
          : item.tenPhanMem,
        value: item.id,
      }));
  });

  // Danh sach ung dung da chon, dung de render dang bang trong dialog thay
  // vi p-multiSelect (kho nhin khi chon nhieu - chu bi cat/chong len nhau).
  readonly selectedUngDungItems = computed<Array<SelectOption<number>>>(() => {
    const ids: number[] = this.ungDungIdsSignal();
    const options = this.heThongOptions();
    return ids
      .map((id) => options.find((option) => option.value === id))
      .filter((option): option is SelectOption<number> => !!option);
  });

  onUngDungAutocomplete(event: { query: string }): void {
    const query = event.query.trim().toLowerCase();
    const selectedIds = new Set<number>(
      this.form.controls.ungDungIds.value ?? [],
    );
    const candidates = this.heThongOptions().filter(
      (option) => !selectedIds.has(option.value),
    );
    this.ungDungSuggestions.set(
      (query
        ? candidates.filter((option) =>
            option.label.toLowerCase().includes(query),
          )
        : candidates
      ).map((option) => option.label),
    );
  }

  // Suggestion la chuoi label (khong phai object) - tranh PrimeNG tu dong
  // dong bo lai gia tri input thanh label sau khi chon, xung dot voi viec
  // tu xoa o tim kiem ngay sau addUngDung.
  onUngDungSelect(label: string): void {
    const option = this.heThongOptions().find((o) => o.label === label);
    if (option) {
      const control = this.form.controls.ungDungIds;
      const current: number[] = control.value ?? [];
      if (!current.includes(option.value)) {
        const next = [...current, option.value];
        control.setValue(next);
        control.markAsDirty();
        this.ungDungIdsSignal.set(next);
      }
    }
    this.ungDungSearchQuery.set('');
  }

  removeUngDung(id: number): void {
    const control = this.form.controls.ungDungIds;
    const next = (control.value ?? []).filter((x: number) => x !== id);
    control.setValue(next);
    control.markAsDirty();
    this.ungDungIdsSignal.set(next);
  }

  readonly donViSuDungOptions = computed<Array<SelectOption<string>>>(() => {
    const options = this.flattenDonViOptions(this.donViSuDungTree());
    const selected = (this.form.controls.donViSuDung.value ?? '').trim();
    if (!selected) {
      return options;
    }

    const exists = options.some((item) => item.value === selected);
    if (exists) {
      return options;
    }

    return [{ label: selected, value: selected }, ...options];
  });

  constructor(
    private readonly formBuilder: FormBuilder,
    private readonly thietBiApi: ThietBiCnttApi,
    private readonly donViApi: DonViApi,
    private readonly authService: AuthService,
    private readonly notificationService: NotificationService,
    private readonly confirmDialog: ConfirmDialogWrapperService,
  ) {
    this.form.controls.loaiThietBiId.valueChanges.subscribe((value) => {
      this.selectedLoaiId.set(value);
      this.syncLoaiThietBiMode();
    });
    this.form.controls.soLuongHienDung.valueChanges.subscribe((value) => {
      if (!this.isTongHop()) {
        return;
      }
      this.form.controls.soLuongTong.setValue(Number(value ?? 0), {
        emitEvent: false,
      });
      this.form.controls.soLuongHong.setValue(0, { emitEvent: false });
      this.form.updateValueAndValidity({ emitEvent: false });
    });
    this.form.controls.hangSanXuat.valueChanges.subscribe(() => {
      this.refreshModelSuggestions('');
    });
    void this.initialize();
  }

  async initialize(): Promise<void> {
    this.loading.set(true);
    try {
      const [items, catalog, loaiTree, heThongThongTin, donViTree] =
        await Promise.all([
          this.thietBiApi.getAll(),
          this.thietBiApi.getCatalog(),
          this.thietBiApi.getLoaiThietBiTree(),
          this.thietBiApi.getHeThongThongTin(),
          this.donViApi.getTree(),
        ]);

      this.items.set(items);
      this.applyCatalog(catalog);
      this.loaiThietBiTree.set(loaiTree);
      this.loaiThietBiOptions.set(this.flattenLoaiThietBiTree(loaiTree));
      this.loaiThietBiTreeOptions.set(this.buildLoaiThietBiTreeNodes(loaiTree));
      this.heThongThongTin.set(heThongThongTin);
      this.donViSuDungTree.set(this.resolveUserDonViSubtree(donViTree));
      this.syncAggregateDrafts();
      this.syncLoaiThietBiMode();
    } finally {
      this.loading.set(false);
    }
  }

  async load(): Promise<void> {
    this.loading.set(true);
    try {
      const [items, catalog] = await Promise.all([
        this.thietBiApi.getAll(),
        this.thietBiApi.getCatalog(),
      ]);
      this.items.set(items);
      this.applyCatalog(catalog);
      this.syncAggregateDrafts();
    } finally {
      this.loading.set(false);
    }
  }

  onModelAutocomplete(event: { query?: string }): void {
    this.refreshModelSuggestions(event.query ?? '');
  }

  onHangSanXuatAutocomplete(event: { query?: string }): void {
    this.refreshSimpleSuggestions(
      this.hangSanXuatCatalog(),
      event.query ?? '',
      this.hangSanXuatSuggestions,
    );
  }

  onHeDieuHanhAutocomplete(event: { query?: string }): void {
    this.refreshSimpleSuggestions(
      this.heDieuHanhCatalog(),
      event.query ?? '',
      this.heDieuHanhSuggestions,
    );
  }

  resetFilters(): void {
    this.filterLoaiThietBiId.set(null);
    this.filterTenThietBi.set('');
  }

  hasSoLuongError(): boolean {
    return this.form.hasError('soLuongVuotQua') && this.form.touched;
  }

  dialogTitle(): string {
    if (this.isTongHop()) {
      return this.selectedId()
        ? 'Cập nhật số lượng thiết bị'
        : 'Khai báo số lượng thiết bị';
    }
    return this.selectedId() ? 'Cập nhật thiết bị' : 'Thêm thiết bị chi tiết';
  }

  dialogSubmitLabel(): string {
    if (this.isTongHop()) {
      return this.selectedId() ? 'Lưu thay đổi' : 'Lưu số lượng';
    }
    return this.selectedId() ? 'Lưu thay đổi' : 'Thêm thiết bị';
  }

  openCreateDialog(): void {
    this.resetForm();
    this.formDialogVisible.set(true);
  }

  openCreateForCategory(category: ThietBiCategoryView): void {
    this.resetForm();
    const defaultQuantity = category.laTongHop ? 0 : 1;
    this.form.patchValue({
      loaiThietBiId: category.id,
      soLuongTong: defaultQuantity,
      soLuongHienDung: defaultQuantity,
      soLuongHong: 0,
    });
    this.syncLoaiThietBiMode();
    this.formDialogVisible.set(true);
  }

  async openCategoryEditor(category: ThietBiCategoryView): Promise<void> {
    if (category.primaryItem) {
      await this.openEditDialog(category.primaryItem);
      return;
    }
    this.openCreateForCategory(category);
  }

  async openEditDialog(item: ThietBiCnttDto): Promise<void> {
    await this.select(item);
    this.formDialogVisible.set(true);
  }

  closeDialog(): void {
    this.formDialogVisible.set(false);
  }

  isGroupExpanded(key: string): boolean {
    return this.expandedGroups()[key] ?? true;
  }

  toggleGroup(key: string): void {
    const current = this.expandedGroups();
    this.expandedGroups.set({
      ...current,
      [key]: !(current[key] ?? true),
    });
  }

  isCategoryExpanded(key: string): boolean {
    return this.expandedRows()[key] ?? true;
  }

  toggleCategory(key: string): void {
    const current = this.expandedRows();
    this.expandedRows.set({
      ...current,
      [key]: !(current[key] ?? true),
    });
  }

  setAllExpanded(expanded: boolean): void {
    const groupState: Record<string, boolean> = {};
    const categoryState: Record<string, boolean> = {};
    for (const group of this.deviceGroups()) {
      groupState[group.key] = expanded;
      for (const category of group.categories) {
        categoryState[category.key] = expanded;
      }
    }
    this.expandedGroups.set(groupState);
    this.expandedRows.set(categoryState);
  }

  aggregateDraft(category: ThietBiCategoryView): AggregateDraft {
    return (
      this.aggregateDrafts()[category.id] ?? {
        soLuongHienDung: category.currentCount,
        tinhTrang: category.primaryItem?.tinhTrang ?? '',
        ghiChu: category.primaryItem?.ghiChu ?? '',
      }
    );
  }

  updateAggregateDraft(
    category: ThietBiCategoryView,
    patch: Partial<AggregateDraft>,
  ): void {
    const current = this.aggregateDraft(category);
    this.aggregateDrafts.update((drafts) => ({
      ...drafts,
      [category.id]: {
        ...current,
        ...patch,
        soLuongHienDung: Math.max(
          0,
          Number(patch.soLuongHienDung ?? current.soLuongHienDung),
        ),
      },
    }));
  }

  adjustAggregateDraft(
    category: ThietBiCategoryView,
    delta: number,
  ): void {
    this.updateAggregateDraft(category, {
      soLuongHienDung: this.aggregateDraft(category).soLuongHienDung + delta,
    });
  }

  adjustFormQuantity(delta: number): void {
    const current = Number(this.form.controls.soLuongHienDung.value ?? 0);
    this.form.controls.soLuongHienDung.setValue(Math.max(0, current + delta));
    this.form.controls.soLuongHienDung.markAsDirty();
  }

  hasAggregateChanges(category: ThietBiCategoryView): boolean {
    const draft = this.aggregateDraft(category);
    return (
      draft.soLuongHienDung !== category.currentCount ||
      draft.tinhTrang.trim() !== (category.primaryItem?.tinhTrang ?? '').trim() ||
      draft.ghiChu.trim() !== (category.primaryItem?.ghiChu ?? '').trim()
    );
  }

  async saveAggregate(category: ThietBiCategoryView): Promise<void> {
    if (this.savingAggregateId() !== null) {
      return;
    }

    const donViId = this.donViId();
    if (!donViId) {
      this.notificationService.show(
        'error',
        'Không xác định được đơn vị. Vui lòng đăng nhập lại.',
      );
      return;
    }

    const draft = this.aggregateDraft(category);
    const quantity = Math.max(0, draft.soLuongHienDung);
    const current = category.primaryItem;
    this.savingAggregateId.set(category.id);
    try {
      const payload: UpsertThietBiCnttRequest = {
        donViId,
        loaiThietBiId: category.id,
        tenThietBi: null,
        hangSanXuat: null,
        model: null,
        cauHinh: null,
        heDieuHanh: null,
        donViSuDung: null,
        soLuongTong: quantity,
        soLuongHienDung: quantity,
        soLuongHong: 0,
        tinhTrang: this.normalizeText(draft.tinhTrang),
        ghiChu: this.normalizeText(draft.ghiChu),
        ungDungIds: [],
      };

      if (current) {
        await this.thietBiApi.update(current.id, payload);
      } else {
        await this.thietBiApi.create(payload);
      }
      this.notificationService.show(
        'success',
        `Đã lưu thông tin ${category.label}.`,
      );
      await this.load();
    } finally {
      this.savingAggregateId.set(null);
    }
  }

  async save(): Promise<void> {
    if (this.form.invalid || this.saving()) {
      this.form.markAllAsTouched();
      return;
    }

    const donViId = this.donViId();
    if (!donViId) {
      this.notificationService.show(
        'error',
        'Không xác định được đơn vị. Vui lòng đăng nhập lại.',
      );
      return;
    }

    this.saving.set(true);
    try {
      const raw = this.form.getRawValue();
      const soLuongHienDung = Number(raw.soLuongHienDung ?? 0);
      const soLuongHong = Math.max(0, Number(raw.soLuongHong ?? 0));
      const soLuongTong = Math.max(
        Number(raw.soLuongTong ?? 0),
        soLuongHienDung + soLuongHong,
      );

      const payload: UpsertThietBiCnttRequest = {
        donViId,
        loaiThietBiId: Number(raw.loaiThietBiId),
        tenThietBi: this.isTongHop()
          ? null
          : this.normalizeText(raw.tenThietBi),
        hangSanXuat: this.isTongHop()
          ? null
          : this.normalizeText(raw.hangSanXuat),
        model: this.isTongHop() ? null : this.normalizeText(raw.model),
        cauHinh: this.isTongHop() ? null : this.normalizeText(raw.cauHinh),
        heDieuHanh: this.isTongHop()
          ? null
          : this.normalizeText(raw.heDieuHanh),
        donViSuDung: this.isTongHop()
          ? null
          : this.normalizeText(raw.donViSuDung),
        soLuongTong,
        soLuongHienDung,
        soLuongHong,
        tinhTrang: this.normalizeText(raw.tinhTrang),
        ghiChu: this.normalizeText(raw.ghiChu),
        ungDungIds: this.isTongHop()
          ? []
          : (raw.ungDungIds ?? []).map((value) => Number(value)),
      };

      if (this.selectedId()) {
        await this.thietBiApi.update(this.selectedId()!, payload);
        this.notificationService.show(
          'success',
          'Cập nhật thiết bị thành công.',
        );
      } else {
        await this.thietBiApi.create(payload);
        this.notificationService.show(
          'success',
          'Tạo mới thiết bị thành công.',
        );
      }

      this.resetForm();
      this.closeDialog();
      await this.load();
    } finally {
      this.saving.set(false);
    }
  }

  async select(item: ThietBiCnttDto): Promise<void> {
    const detail = await this.thietBiApi.getById(item.id);
    this.selectedId.set(detail.id);
    this.form.patchValue({
      loaiThietBiId: detail.loaiThietBiId,
      tenThietBi: detail.tenThietBi ?? '',
      hangSanXuat: detail.hangSanXuat ?? '',
      model: detail.model ?? '',
      cauHinh: detail.cauHinh ?? '',
      heDieuHanh: detail.heDieuHanh ?? '',
      donViSuDung: detail.donViSuDung ?? '',
      soLuongTong: detail.soLuongTong,
      soLuongHienDung: detail.soLuongHienDung,
      soLuongHong: detail.soLuongHong,
      tinhTrang: detail.tinhTrang ?? '',
      ghiChu: detail.ghiChu ?? '',
      ungDungIds: detail.ungDungIds,
    });
    this.ungDungIdsSignal.set(detail.ungDungIds ?? []);
    this.refreshModelSuggestions(detail.model ?? '');
    this.syncLoaiThietBiMode();
    this.ungDungSearchQuery.set('');
    this.ungDungSuggestions.set([]);
  }

  async remove(item: ThietBiCnttDto): Promise<void> {
    const confirmed = await this.confirmDialog.confirmDelete({
      message: `Xác nhận xóa thiết bị ${this.resolveDisplayName(item)}?`,
      acceptLabel: 'Xóa',
      rejectLabel: 'Hủy',
    });
    if (!confirmed) {
      return;
    }

    await this.thietBiApi.delete(item.id);
    this.notificationService.show('success', 'Xóa thiết bị thành công.');
    if (this.selectedId() === item.id) {
      this.resetForm();
    }
    await this.load();
  }

  resetForm(): void {
    this.selectedId.set(null);
    this.form.reset({
      loaiThietBiId: null,
      tenThietBi: '',
      hangSanXuat: '',
      model: '',
      cauHinh: '',
      heDieuHanh: '',
      donViSuDung: '',
      soLuongTong: 0,
      soLuongHienDung: 0,
      soLuongHong: 0,
      tinhTrang: '',
      ghiChu: '',
      ungDungIds: [],
    });
    this.ungDungIdsSignal.set([]);
    this.refreshModelSuggestions('');
    this.syncLoaiThietBiMode();
    this.ungDungSearchQuery.set('');
    this.ungDungSuggestions.set([]);
  }

  resolveLoaiThietBiLabel(loaiThietBiId: number): string {
    return (
      this.loaiThietBiOptions().find((item) => item.value === loaiThietBiId)
        ?.label ?? `${loaiThietBiId}`
    );
  }

  resolveUngDungLabels(ids: number[]): string {
    if (ids.length === 0) {
      return '—';
    }
    const names = this.heThongThongTin()
      .filter((item) => ids.includes(item.id))
      .map((item) => item.tenPhanMem);
    return names.length > 0 ? names.join(', ') : ids.join(', ');
  }

  displayDeviceName(item: ThietBiCnttDto): string {
    const name = item.tenThietBi?.trim();
    if (!name) {
      return this.cleanCatalogLabel(
        this.resolveLoaiThietBiLabel(item.loaiThietBiId),
      );
    }
    return name.replace(/^\(\d+\)\s*/, '');
  }

  manufacturerLabel(item: ThietBiCnttDto): string {
    return [item.hangSanXuat, item.model].filter(Boolean).join(' · ') || '—';
  }

  toRoman(value: number): string {
    const map: Array<{ value: number; symbol: string }> = [
      { value: 1000, symbol: 'M' },
      { value: 900, symbol: 'CM' },
      { value: 500, symbol: 'D' },
      { value: 400, symbol: 'CD' },
      { value: 100, symbol: 'C' },
      { value: 90, symbol: 'XC' },
      { value: 50, symbol: 'L' },
      { value: 40, symbol: 'XL' },
      { value: 10, symbol: 'X' },
      { value: 9, symbol: 'IX' },
      { value: 5, symbol: 'V' },
      { value: 4, symbol: 'IV' },
      { value: 1, symbol: 'I' },
    ];
    let remaining = Math.max(1, Math.floor(value));
    let result = '';
    for (const item of map) {
      while (remaining >= item.value) {
        result += item.symbol;
        remaining -= item.value;
      }
    }
    return result;
  }

  // De quy - cay khong gioi han so cap, chi lay node "la" (khong co con)
  // lam lua chon duoc phep, khop voi rang buoc phia backend (EnsureLoaiThietBiAsync).
  private flattenLoaiThietBiTree(
    tree: RefLoaiThietBiDto[],
    ancestorLabels: string[] = [],
  ): LoaiThietBiOption[] {
    return this.sortLoaiNodes(tree).flatMap((node) => {
      const pathLabels = [...ancestorLabels, node.tenLoai];
      if (node.children.length > 0) {
        return this.flattenLoaiThietBiTree(node.children, pathLabels);
      }

      const label = pathLabels.join(' / ');
      return [
        {
          label,
          searchLabel: this.toSearchLabel(label),
          value: node.id,
          laTongHop: node.laTongHop,
        },
      ];
    });
  }

  private toSearchLabel(value: string): string {
    return value.replace(/đ/g, 'd').replace(/Đ/g, 'D');
  }

  // Cay cho p-treeSelect - mac dinh dong (expanded: false) de gon, chi cho
  // chon node "la" (selectable = khong co con), khop dung rang buoc backend.
  private buildLoaiThietBiTreeNodes(
    tree: RefLoaiThietBiDto[],
  ): TreeNode<{ id: number; laTongHop: boolean }>[] {
    return this.sortLoaiNodes(tree).map((node) => ({
      key: String(node.id),
      label: node.tenLoai,
      data: { id: node.id, laTongHop: node.laTongHop },
      selectable: node.children.length === 0,
      expanded: false,
      children:
        node.children.length > 0
          ? this.buildLoaiThietBiTreeNodes(node.children)
          : [],
    }));
  }

  private findLoaiThietBiTreeNode(
    nodes: TreeNode<{ id: number; laTongHop: boolean }>[],
    id: number,
  ): TreeNode<{ id: number; laTongHop: boolean }> | null {
    for (const node of nodes) {
      if (node.data?.id === id) {
        return node;
      }
      const found = this.findLoaiThietBiTreeNode(node.children ?? [], id);
      if (found) {
        return found;
      }
    }
    return null;
  }

  onLoaiThietBiTreeSelect(
    node: TreeNode<{ id: number; laTongHop: boolean }> | null,
  ): void {
    const control = this.form.controls.loaiThietBiId;
    control.setValue(node?.data?.id ?? null);
    control.markAsDirty();
    control.markAsTouched();
  }

  private syncLoaiThietBiMode(): void {
    const donViSuDungControl = this.form.controls.donViSuDung;
    const tenThietBiControl = this.form.controls.tenThietBi;

    if (this.isTongHop()) {
      donViSuDungControl.clearValidators();
      tenThietBiControl.clearValidators();
      this.form.patchValue(
        {
          tenThietBi: '',
          hangSanXuat: '',
          model: '',
          cauHinh: '',
          heDieuHanh: '',
          donViSuDung: '',
          soLuongTong: Number(this.form.controls.soLuongHienDung.value ?? 0),
          soLuongHong: 0,
          ungDungIds: [],
        },
        { emitEvent: false },
      );
      this.ungDungIdsSignal.set([]);
    } else {
      donViSuDungControl.setValidators([Validators.required]);
      tenThietBiControl.setValidators([Validators.required]);
    }

    donViSuDungControl.updateValueAndValidity({ emitEvent: false });
    tenThietBiControl.updateValueAndValidity({ emitEvent: false });
    this.form.updateValueAndValidity({ emitEvent: false });
  }

  private resolveDisplayName(item: ThietBiCnttDto): string {
    return item.tenThietBi ?? this.resolveLoaiThietBiLabel(item.loaiThietBiId);
  }

  private normalizeText(value: string | null | undefined): string | null {
    const normalized = value?.trim();
    return normalized ? normalized : null;
  }


  // Gợi ý Hãng SX/Model/HĐH lấy từ danh sách dùng chung toàn hệ thống trả về
  // bởi server (GET /thiet-bi-cntt/catalog) — không còn phụ thuộc localStorage
  // riêng từng trình duyệt hay chỉ dữ liệu của đơn vị mình, để các đơn vị khác
  // nhau nhập cùng 1 tập giá trị đã dùng, hạn chế trùng lặp do gõ khác nhau.
  private applyCatalog(catalog: ThietBiCatalogDto): void {
    this.hangSanXuatCatalog.set(this.sortTextArray(catalog.hangSanXuat ?? []));
    this.heDieuHanhCatalog.set(this.sortTextArray(catalog.heDieuHanh ?? []));
    this.modelCatalogGlobal.set(this.sortTextArray(catalog.modelGlobal ?? []));
    this.modelCatalogByHang.set(catalog.modelByHang ?? {});
    this.refreshModelSuggestions(this.form.controls.model.value ?? '');
    this.refreshSimpleSuggestions(
      this.hangSanXuatCatalog(),
      this.form.controls.hangSanXuat.value ?? '',
      this.hangSanXuatSuggestions,
    );
    this.refreshSimpleSuggestions(
      this.heDieuHanhCatalog(),
      this.form.controls.heDieuHanh.value ?? '',
      this.heDieuHanhSuggestions,
    );
  }

  private refreshModelSuggestions(query: string): void {
    const byHang = this.resolveModelCandidatesByCurrentHang();
    const fallback = this.modelCatalogGlobal();
    const merged = this.mergeUnique(byHang, fallback);
    const q = query.trim().toLowerCase();

    const currentModel = (this.form.controls.model.value ?? '').trim();
    const withCurrent = currentModel
      ? this.mergeUnique([currentModel], merged)
      : merged;

    this.modelSuggestions.set(
      q
        ? withCurrent
            .filter((value) => value.toLowerCase().includes(q))
            .slice(0, 50)
        : withCurrent.slice(0, 50),
    );
  }

  private refreshSimpleSuggestions(
    catalog: string[],
    query: string,
    target: WritableSignal<string[]>,
  ): void {
    const q = query.trim().toLowerCase();
    target.set(
      q
        ? catalog.filter((value) => value.toLowerCase().includes(q)).slice(0, 50)
        : catalog.slice(0, 50),
    );
  }

  private resolveModelCandidatesByCurrentHang(): string[] {
    const hang = (this.form.controls.hangSanXuat.value ?? '').trim();
    const key = this.normalizeLookup(hang);
    if (!key) {
      return [];
    }

    return this.modelCatalogByHang()[key] ?? [];
  }

  private mergeUnique(primary: string[], secondary: string[]): string[] {
    const dedup = new Map<string, string>();
    for (const value of [...primary, ...secondary]) {
      const normalized = this.normalizeText(value);
      if (!normalized) {
        continue;
      }

      const key = normalized.toLowerCase();
      if (!dedup.has(key)) {
        dedup.set(key, normalized);
      }
    }

    return this.sortTextArray(Array.from(dedup.values()));
  }

  private sortTextArray(values: string[]): string[] {
    return [...values].sort((a, b) =>
      a.localeCompare(b, 'vi', { sensitivity: 'base' }),
    );
  }

  private normalizeLookup(value: string): string {
    return value.trim().toLowerCase();
  }

  private collectLeafNodes(nodes: RefLoaiThietBiDto[]): RefLoaiThietBiDto[] {
    return this.sortLoaiNodes(nodes).flatMap((node) =>
      node.children.length > 0 ? this.collectLeafNodes(node.children) : [node],
    );
  }

  private sortLoaiNodes(nodes: RefLoaiThietBiDto[]): RefLoaiThietBiDto[] {
    return [...nodes].sort(
      (a, b) =>
        a.sortOrder - b.sortOrder ||
        a.tenLoai.localeCompare(b.tenLoai, 'vi', { sensitivity: 'base' }),
    );
  }

  private cleanCatalogLabel(value: string): string {
    const lastPart = value.split('/').at(-1)?.trim() ?? value.trim();
    return lastPart.replace(/^\s*(?:[IVXLCDM]+|\d+)[.)]\s*/i, '').trim();
  }

  private normalizeSearch(value: string): string {
    return value
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .replace(/đ/g, 'd')
      .replace(/Đ/g, 'D')
      .trim()
      .toLowerCase();
  }

  private matchesText(value: string | null | undefined, query: string): boolean {
    return !query || this.normalizeSearch(value ?? '').includes(query);
  }

  private matchesDeviceItem(item: ThietBiCnttDto, query: string): boolean {
    const applications = this.resolveUngDungLabels(item.ungDungIds);
    return this.matchesText(
      [
        item.tenThietBi,
        item.hangSanXuat,
        item.model,
        item.cauHinh,
        item.heDieuHanh,
        item.donViSuDung,
        item.tinhTrang,
        item.ghiChu,
        applications,
      ]
        .filter(Boolean)
        .join(' '),
      query,
    );
  }

  private isLoaiTongHop(loaiThietBiId: number): boolean {
    return (
      this.loaiThietBiOptions().find(
        (option) => option.value === loaiThietBiId,
      )?.laTongHop ?? false
    );
  }

  private syncAggregateDrafts(): void {
    const drafts: Record<number, AggregateDraft> = {};
    for (const option of this.loaiThietBiOptions()) {
      if (!option.laTongHop) {
        continue;
      }
      const categoryItems = this.items().filter(
        (item) => item.loaiThietBiId === option.value,
      );
      drafts[option.value] = {
        soLuongHienDung: categoryItems.reduce(
          (sum, item) => sum + item.soLuongHienDung,
          0,
        ),
        tinhTrang: categoryItems[0]?.tinhTrang ?? '',
        ghiChu: categoryItems[0]?.ghiChu ?? '',
      };
    }
    this.aggregateDrafts.set(drafts);
  }

  private resolveUserDonViSubtree(tree: DonViDto[]): DonViDto[] {
    const currentDonViId = this.donViId();
    if (!currentDonViId) {
      return [];
    }

    const node = this.findDonViNode(tree, currentDonViId);
    return node ? [node] : [];
  }

  private findDonViNode(items: DonViDto[], targetId: number): DonViDto | null {
    for (const item of items) {
      if (item.id === targetId) {
        return item;
      }

      const children = Array.isArray(item.children) ? item.children : [];
      const found = this.findDonViNode(children, targetId);
      if (found) {
        return found;
      }
    }

    return null;
  }

  private flattenDonViOptions(
    items: DonViDto[],
    level = 0,
  ): Array<SelectOption<string>> {
    const result: Array<SelectOption<string>> = [];

    for (const item of items) {
      const prefix = level > 0 ? `${'--'.repeat(level)} ` : '';
      result.push({
        label: `${prefix}${item.tenDonVi} (${item.maDonVi})`,
        value: item.tenDonVi,
      });

      const children = Array.isArray(item.children) ? item.children : [];
      result.push(...this.flattenDonViOptions(children, level + 1));
    }

    return result;
  }
}
