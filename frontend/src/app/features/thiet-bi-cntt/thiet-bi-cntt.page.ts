import { CommonModule } from '@angular/common';
import { Component, computed, signal } from '@angular/core';
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
import { MultiSelectModule } from 'primeng/multiselect';
import { TableModule } from 'primeng/table';
import { TooltipModule } from 'primeng/tooltip';
import { DialogModule } from 'primeng/dialog';
import { AuthService } from '../../core/auth/auth.service';
import { NotificationService } from '../../core/ui/notification.service';
import { DonViApi, DonViDto } from '../don-vi/don-vi.api';
import { ConfirmDialogWrapperService } from '../../shared/ui/confirm-dialog-wrapper.service';
import { EmptyStateComponent } from '../../shared/ui/empty-state.component';
import { FilterBarComponent } from '../../shared/ui/filter-bar.component';
import { FormActionBarComponent } from '../../shared/ui/form-action-bar.component';
import { LoadingOverlayComponent } from '../../shared/ui/loading-overlay.component';
import {
  APP_MULTISELECT_PANEL_STYLE_CLASS,
  APP_SELECT_PANEL_STYLE_CLASS,
  APP_TABLE_BODY_CELL_CLASS,
  APP_TABLE_HEADER_CELL_CLASS,
  APP_TABLE_ROW_CLASS,
  APP_TABLE_STYLE_CLASS,
} from '../../shared/ui/primeng-pt';
import { SectionCardComponent } from '../../shared/ui/section-card.component';
import {
  HeThongThongTinOptionDto,
  RefLoaiThietBiDto,
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
  value: number;
  laTongHop: boolean;
}

interface GroupedThietBiRow {
  item: ThietBiCnttDto;
  children: ThietBiCnttDto[];
  key: string;
}

interface GroupedThietBiItems {
  index: number;
  parentLabel: string;
  rows: GroupedThietBiRow[];
}

interface QuickAddDraft {
  tenThietBi: string;
  soLuongHienDung: number;
  tinhTrang: string;
}

interface ThietBiCatalogCache {
  hangSanXuat: string[];
  heDieuHanh: string[];
  modelByHang: Record<string, string[]>;
  modelGlobal: string[];
}

@Component({
  selector: 'app-thiet-bi-cntt-page',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    SectionCardComponent,
    FilterBarComponent,
    FormActionBarComponent,
    EmptyStateComponent,
    LoadingOverlayComponent,
    AutoCompleteModule,
    DropdownModule,
    MultiSelectModule,
    InputNumberModule,
    InputTextModule,
    ButtonModule,
    TableModule,
    TooltipModule,
    DialogModule,
  ],
  templateUrl: './thiet-bi-cntt.page.html',
  styleUrl: './thiet-bi-cntt.page.scss',
})
export class ThietBiCnttPage {
  private static readonly CATALOG_CACHE_KEY = 'thiet_bi_cntt_catalog_v1';

  readonly selectPanelStyleClass = APP_SELECT_PANEL_STYLE_CLASS;
  readonly multiSelectPanelStyleClass = APP_MULTISELECT_PANEL_STYLE_CLASS;
  readonly tableStyleClass = APP_TABLE_STYLE_CLASS;
  readonly tableHeaderCellClass = APP_TABLE_HEADER_CELL_CLASS;
  readonly tableRowClass = APP_TABLE_ROW_CLASS;
  readonly tableBodyCellClass = APP_TABLE_BODY_CELL_CLASS;

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

  readonly selectedLoaiThietBi = computed(() => {
    const loaiThietBiId = this.form.controls.loaiThietBiId.value;
    return (
      this.loaiThietBiOptions().find((item) => item.value === loaiThietBiId) ??
      null
    );
  });

  readonly isTongHop = computed(
    () => this.selectedLoaiThietBi()?.laTongHop ?? false,
  );

  readonly soLuongError = computed(
    () => this.form.hasError('soLuongVuotQua') && this.form.touched,
  );

  items = signal<ThietBiCnttDto[]>([]);
  loading = signal(false);
  saving = signal(false);
  formDialogVisible = signal(false);
  selectedId = signal<number | null>(null);
  loaiThietBiOptions = signal<LoaiThietBiOption[]>([]);
  heThongThongTin = signal<HeThongThongTinOptionDto[]>([]);
  donViSuDungTree = signal<DonViDto[]>([]);
  hangSanXuatCatalog = signal<string[]>([]);
  heDieuHanhCatalog = signal<string[]>([]);
  modelCatalogByHang = signal<Record<string, string[]>>({});
  modelCatalogGlobal = signal<string[]>([]);
  modelSuggestions = signal<string[]>([]);
  expandedGroups = signal<Record<string, boolean>>({});
  expandedRows = signal<Record<string, boolean>>({});
  quickAddDrafts = signal<Record<string, QuickAddDraft>>({});

  filterLoaiThietBiId = signal<number | null>(null);
  filterTenThietBi = signal<string>('');

  readonly filteredItems = computed(() => {
    const loai = this.filterLoaiThietBiId();
    const ten = this.filterTenThietBi().trim().toLowerCase();
    return this.items().filter(
      (item) =>
        (loai === null || item.loaiThietBiId === loai) &&
        (!ten || (item.tenThietBi ?? '').toLowerCase().includes(ten)),
    );
  });

  readonly groupedFilteredItems = computed<GroupedThietBiItems[]>(() => {
    const groups = new Map<string, ThietBiCnttDto[]>();
    for (const item of this.filteredItems()) {
      const parent = this.resolveParentLoaiLabel(item);
      if (!groups.has(parent)) {
        groups.set(parent, []);
      }
      groups.get(parent)!.push(item);
    }

    return Array.from(groups.entries()).map(([parentLabel, items], idx) => {
      const rows: GroupedThietBiRow[] = [];
      for (const item of items) {
        if (this.isChildName(item.tenThietBi) && rows.length > 0) {
          rows[rows.length - 1].children.push(item);
          continue;
        }

        rows.push({
          item,
          children: [],
          key: `${parentLabel}-${item.id}`,
        });
      }

      return {
        index: idx + 1,
        parentLabel,
        rows,
      };
    });
  });

  readonly selectedLoaiKeyword = computed(() => {
    const label = this.selectedLoaiThietBi()?.label?.toLowerCase() ?? '';
    return {
      isMayChu: label.includes('máy chủ') || label.includes('server'),
      isMang:
        label.includes('router') ||
        label.includes('switch') ||
        label.includes('tường lửa') ||
        label.includes('mạng'),
      isBaoMat: label.includes('bảo mật') || label.includes('security'),
    };
  });

  readonly visibleDialogFields = computed(() => {
    const k = this.selectedLoaiKeyword();
    if (k.isMayChu) {
      return {
        showModel: true,
        showCauHinh: true,
        showHeDieuHanh: true,
        showDonViSuDung: true,
        showUngDung: true,
      };
    }
    if (k.isMang) {
      return {
        showModel: true,
        showCauHinh: true,
        showHeDieuHanh: false,
        showDonViSuDung: true,
        showUngDung: false,
      };
    }
    if (k.isBaoMat) {
      return {
        showModel: true,
        showCauHinh: true,
        showHeDieuHanh: false,
        showDonViSuDung: true,
        showUngDung: false,
      };
    }
    return {
      showModel: true,
      showCauHinh: true,
      showHeDieuHanh: true,
      showDonViSuDung: true,
      showUngDung: true,
    };
  });

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

  readonly hangSanXuatOptions = computed<Array<SelectOption<string>>>(() => {
    return this.withCurrentValueAsOption(
      this.hangSanXuatCatalog(),
      this.form.controls.hangSanXuat.value ?? '',
    );
  });

  readonly heDieuHanhOptions = computed<Array<SelectOption<string>>>(() => {
    return this.withCurrentValueAsOption(
      this.heDieuHanhCatalog(),
      this.form.controls.heDieuHanh.value ?? '',
    );
  });

  constructor(
    private readonly formBuilder: FormBuilder,
    private readonly thietBiApi: ThietBiCnttApi,
    private readonly donViApi: DonViApi,
    private readonly authService: AuthService,
    private readonly notificationService: NotificationService,
    private readonly confirmDialog: ConfirmDialogWrapperService,
  ) {
    this.form.controls.loaiThietBiId.valueChanges.subscribe(() => {
      this.syncLoaiThietBiMode();
    });
    this.form.controls.hangSanXuat.valueChanges.subscribe(() => {
      this.refreshModelSuggestions('');
    });
    void this.initialize();
  }

  async initialize(): Promise<void> {
    this.loading.set(true);
    try {
      const [items, loaiTree, heThongThongTin, donViTree] = await Promise.all([
        this.thietBiApi.getAll(),
        this.thietBiApi.getLoaiThietBiTree(),
        this.thietBiApi.getHeThongThongTin(),
        this.donViApi.getTree(),
      ]);

      this.items.set(items);
      this.refreshCatalogs(items);
      this.loaiThietBiOptions.set(this.flattenLoaiThietBiTree(loaiTree));
      this.heThongThongTin.set(heThongThongTin);
      this.donViSuDungTree.set(this.resolveUserDonViSubtree(donViTree));
      this.syncLoaiThietBiMode();
    } finally {
      this.loading.set(false);
    }
  }

  async load(): Promise<void> {
    this.loading.set(true);
    try {
      const items = await this.thietBiApi.getAll();
      this.items.set(items);
      this.refreshCatalogs(items);
    } finally {
      this.loading.set(false);
    }
  }

  onModelAutocomplete(event: { query?: string }): void {
    this.refreshModelSuggestions(event.query ?? '');
  }

  resetFilters(): void {
    this.filterLoaiThietBiId.set(null);
    this.filterTenThietBi.set('');
  }

  openCreateDialog(): void {
    this.resetForm();
    this.formDialogVisible.set(true);
  }

  async openEditDialog(item: ThietBiCnttDto): Promise<void> {
    await this.select(item);
    this.formDialogVisible.set(true);
  }

  closeDialog(): void {
    this.formDialogVisible.set(false);
  }

  isGroupExpanded(parentLabel: string): boolean {
    return this.expandedGroups()[parentLabel] ?? true;
  }

  toggleGroup(parentLabel: string): void {
    const current = this.expandedGroups();
    this.expandedGroups.set({
      ...current,
      [parentLabel]: !(current[parentLabel] ?? true),
    });
  }

  isRowExpanded(key: string): boolean {
    return this.expandedRows()[key] ?? false;
  }

  toggleRow(key: string): void {
    const current = this.expandedRows();
    this.expandedRows.set({
      ...current,
      [key]: !(current[key] ?? false),
    });
  }

  quickAddDraft(parentLabel: string): QuickAddDraft {
    return (
      this.quickAddDrafts()[parentLabel] ?? {
        tenThietBi: '',
        soLuongHienDung: 1,
        tinhTrang: '',
      }
    );
  }

  updateQuickAddDraft(
    parentLabel: string,
    patch: Partial<QuickAddDraft>,
  ): void {
    const drafts = this.quickAddDrafts();
    const current = this.quickAddDraft(parentLabel);
    this.quickAddDrafts.set({
      ...drafts,
      [parentLabel]: {
        ...current,
        ...patch,
      },
    });
  }

  async addQuickItem(parentLabel: string): Promise<void> {
    const draft = this.quickAddDraft(parentLabel);
    const ten = draft.tenThietBi.trim();
    if (!ten || this.saving()) {
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

    const loaiThietBiId = this.resolveLoaiByParent(parentLabel);
    if (!loaiThietBiId) {
      this.notificationService.show(
        'error',
        'Không xác định được loại thiết bị.',
      );
      return;
    }

    const soLuongHienDung = Number(draft.soLuongHienDung ?? 0);
    this.saving.set(true);
    try {
      const payload: UpsertThietBiCnttRequest = {
        donViId,
        loaiThietBiId,
        tenThietBi: ten,
        hangSanXuat: null,
        model: null,
        cauHinh: null,
        heDieuHanh: null,
        donViSuDung: null,
        soLuongTong: soLuongHienDung,
        soLuongHienDung,
        soLuongHong: 0,
        tinhTrang: this.normalizeText(draft.tinhTrang),
        ghiChu: null,
        ungDungIds: [],
      };

      await this.thietBiApi.create(payload);
      this.notificationService.show(
        'success',
        'Thêm nhanh thiết bị thành công.',
      );
      this.updateQuickAddDraft(parentLabel, {
        tenThietBi: '',
        soLuongHienDung: 1,
        tinhTrang: '',
      });
      await this.load();
    } finally {
      this.saving.set(false);
    }
  }

  fillSampleData(): void {
    const firstNonTongHop = this.loaiThietBiOptions().find((o) => !o.laTongHop);
    const firstDonViSuDung = this.donViSuDungOptions()[0]?.value ?? '';
    this.form.patchValue({
      loaiThietBiId: firstNonTongHop?.value ?? null,
      tenThietBi: 'Máy chủ Dell PowerEdge R740',
      hangSanXuat: 'Dell',
      model: 'PowerEdge R740',
      cauHinh: 'CPU: Intel Xeon Silver 4214R, RAM: 32GB, HDD: 2TB SAS',
      heDieuHanh: 'Windows Server 2019',
      donViSuDung: firstDonViSuDung,
      soLuongTong: 3,
      soLuongHienDung: 2,
      soLuongHong: 1,
      tinhTrang: 'Hoạt động bình thường, 1 máy đang bảo trì',
      ghiChu: '[Dữ liệu mẫu — xóa trước khi dùng thực]',
      ungDungIds: [],
    });
    this.syncLoaiThietBiMode();
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
    this.refreshModelSuggestions(detail.model ?? '');
    this.syncLoaiThietBiMode();
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
    this.refreshModelSuggestions('');
    this.syncLoaiThietBiMode();
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

  private flattenLoaiThietBiTree(
    tree: RefLoaiThietBiDto[],
  ): LoaiThietBiOption[] {
    return tree.flatMap((group) =>
      group.children.map((child) => ({
        label: `${group.tenLoai} / ${child.tenLoai}`,
        value: child.id,
        laTongHop: child.laTongHop,
      })),
    );
  }

  private syncLoaiThietBiMode(): void {
    if (!this.isTongHop()) {
      return;
    }
    this.form.patchValue(
      {
        tenThietBi: '',
        hangSanXuat: '',
        model: '',
        cauHinh: '',
        heDieuHanh: '',
        donViSuDung: '',
        ungDungIds: [],
      },
      { emitEvent: false },
    );
  }

  private resolveDisplayName(item: ThietBiCnttDto): string {
    return item.tenThietBi ?? this.resolveLoaiThietBiLabel(item.loaiThietBiId);
  }

  private resolveParentLoaiLabel(item: ThietBiCnttDto): string {
    const full = this.resolveLoaiThietBiLabel(item.loaiThietBiId);
    const slashIndex = full.indexOf('/');
    return slashIndex >= 0 ? full.slice(0, slashIndex).trim() : full;
  }

  private resolveLoaiByParent(parentLabel: string): number | null {
    const match = this.loaiThietBiOptions().find((option) =>
      option.label.startsWith(`${parentLabel} /`),
    );
    return match?.value ?? null;
  }

  private isChildName(value: string | null | undefined): boolean {
    const text = (value ?? '').trim();
    return /^\(\d+\)/.test(text);
  }

  private normalizeText(value: string | null | undefined): string | null {
    const normalized = value?.trim();
    return normalized ? normalized : null;
  }

  private withCurrentValueAsOption(
    values: string[],
    currentValue: string,
  ): Array<SelectOption<string>> {
    const options = values.map((value) => ({ label: value, value }));
    const current = currentValue.trim();
    if (!current) {
      return options;
    }

    const exists = values.some(
      (value) => value.toLowerCase() === current.toLowerCase(),
    );
    if (exists) {
      return options;
    }

    return [{ label: current, value: current }, ...options];
  }

  private refreshCatalogs(items: ThietBiCnttDto[]): void {
    const extracted = this.extractCatalogFromItems(items);
    const cached = this.readCatalogCache();

    const mergedModelByHang: Record<string, string[]> = {
      ...cached.modelByHang,
    };
    for (const [hangKey, models] of Object.entries(extracted.modelByHang)) {
      mergedModelByHang[hangKey] = this.mergeUnique(
        mergedModelByHang[hangKey] ?? [],
        models,
      );
    }

    const merged: ThietBiCatalogCache = {
      hangSanXuat: this.mergeUnique(cached.hangSanXuat, extracted.hangSanXuat),
      heDieuHanh: this.mergeUnique(cached.heDieuHanh, extracted.heDieuHanh),
      modelByHang: mergedModelByHang,
      modelGlobal: this.mergeUnique(cached.modelGlobal, extracted.modelGlobal),
    };

    this.hangSanXuatCatalog.set(merged.hangSanXuat);
    this.heDieuHanhCatalog.set(merged.heDieuHanh);
    this.modelCatalogByHang.set(merged.modelByHang);
    this.modelCatalogGlobal.set(merged.modelGlobal);
    this.writeCatalogCache(merged);
    this.refreshModelSuggestions(this.form.controls.model.value ?? '');
  }

  private extractCatalogFromItems(
    items: ThietBiCnttDto[],
  ): ThietBiCatalogCache {
    const hang = new Set<string>();
    const os = new Set<string>();
    const modelGlobal = new Set<string>();
    const modelByHangMap = new Map<string, Set<string>>();

    for (const item of items) {
      const hangValue = this.normalizeText(item.hangSanXuat) ?? '';
      const modelValue = this.normalizeText(item.model) ?? '';
      const osValue = this.normalizeText(item.heDieuHanh) ?? '';

      if (hangValue) {
        hang.add(hangValue);
      }

      if (osValue) {
        os.add(osValue);
      }

      if (modelValue) {
        modelGlobal.add(modelValue);
      }

      const hangKey = this.normalizeLookup(hangValue);
      if (hangKey && modelValue) {
        const bucket = modelByHangMap.get(hangKey) ?? new Set<string>();
        bucket.add(modelValue);
        modelByHangMap.set(hangKey, bucket);
      }
    }

    const modelByHang: Record<string, string[]> = {};
    for (const [key, values] of modelByHangMap.entries()) {
      modelByHang[key] = this.sortTextArray(Array.from(values));
    }

    return {
      hangSanXuat: this.sortTextArray(Array.from(hang)),
      heDieuHanh: this.sortTextArray(Array.from(os)),
      modelByHang,
      modelGlobal: this.sortTextArray(Array.from(modelGlobal)),
    };
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

  private readCatalogCache(): ThietBiCatalogCache {
    try {
      const raw = localStorage.getItem(ThietBiCnttPage.CATALOG_CACHE_KEY);
      if (!raw) {
        return this.emptyCatalogCache();
      }

      const parsed = JSON.parse(raw) as Partial<ThietBiCatalogCache>;
      return {
        hangSanXuat: Array.isArray(parsed.hangSanXuat)
          ? this.sortTextArray(
              parsed.hangSanXuat.filter(
                (value): value is string => typeof value === 'string',
              ),
            )
          : [],
        heDieuHanh: Array.isArray(parsed.heDieuHanh)
          ? this.sortTextArray(
              parsed.heDieuHanh.filter(
                (value): value is string => typeof value === 'string',
              ),
            )
          : [],
        modelByHang:
          parsed.modelByHang && typeof parsed.modelByHang === 'object'
            ? Object.fromEntries(
                Object.entries(parsed.modelByHang).map(([key, models]) => [
                  key,
                  Array.isArray(models)
                    ? this.sortTextArray(
                        models.filter(
                          (value): value is string => typeof value === 'string',
                        ),
                      )
                    : [],
                ]),
              )
            : {},
        modelGlobal: Array.isArray(parsed.modelGlobal)
          ? this.sortTextArray(
              parsed.modelGlobal.filter(
                (value): value is string => typeof value === 'string',
              ),
            )
          : [],
      };
    } catch {
      return this.emptyCatalogCache();
    }
  }

  private writeCatalogCache(cache: ThietBiCatalogCache): void {
    try {
      localStorage.setItem(
        ThietBiCnttPage.CATALOG_CACHE_KEY,
        JSON.stringify(cache),
      );
    } catch {
      // ignore storage quota and privacy mode errors
    }
  }

  private emptyCatalogCache(): ThietBiCatalogCache {
    return {
      hangSanXuat: [],
      heDieuHanh: [],
      modelByHang: {},
      modelGlobal: [],
    };
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
