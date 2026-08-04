import { CommonModule } from '@angular/common';
import { Component, HostListener } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import {
  FormsModule,
  UntypedFormBuilder,
  UntypedFormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { PrimeNGConfig } from 'primeng/api';
import { AutoCompleteModule } from 'primeng/autocomplete';
import { ButtonModule } from 'primeng/button';
import { CalendarModule } from 'primeng/calendar';
import { CheckboxModule } from 'primeng/checkbox';
import { DialogModule } from 'primeng/dialog';
import { DropdownModule } from 'primeng/dropdown';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { InputTextareaModule } from 'primeng/inputtextarea';
import { PaginatorModule } from 'primeng/paginator';
import { TableModule } from 'primeng/table';
import { TooltipModule } from 'primeng/tooltip';
import { AuthService } from '../../core/auth/auth.service';
import { DonViModeService } from '../../core/don-vi/don-vi-mode.service';
import { NotificationService } from '../../core/ui/notification.service';
import { HasPermissionDirective } from '../../shared/permission.directive';
import { ConfirmDialogWrapperService } from '../../shared/ui/confirm-dialog-wrapper.service';
import { EmptyStateComponent } from '../../shared/ui/empty-state.component';
import { FilterBarComponent } from '../../shared/ui/filter-bar.component';
import { FormActionBarComponent } from '../../shared/ui/form-action-bar.component';
import { LoadingOverlayComponent } from '../../shared/ui/loading-overlay.component';
import {
  APP_DIALOG_CONTENT_STYLE_CLASS,
  APP_DIALOG_STYLE_CLASS,
  APP_DIALOG_MASK_STYLE_CLASS,
  APP_SELECT_PANEL_STYLE_CLASS,
  APP_TABLE_BODY_CELL_CLASS,
  APP_TABLE_HEADER_CELL_CLASS,
  APP_TABLE_ROW_CLASS,
  APP_TABLE_STYLE_CLASS,
} from '../../shared/ui/primeng-pt';
import { SectionCardComponent } from '../../shared/ui/section-card.component';
import { CodesApi, CodeValueDto } from '../codes/codes.api';
import { KyBaoCaoApi, KyBaoCaoDto } from '../ky-bao-cao/ky-bao-cao.api';
import { SnapshotApi, SnapshotDto } from '../snapshot/snapshot.api';
import {
  BizRecordDto,
  BusinessModulesApi,
  BizValue,
} from './business-modules.api';
import {
  BIZ_MODULES,
  BizFieldConfig,
  BizModuleConfig,
} from './business-modules.config';

interface HocVienGridRow {
  localId: string;
  id: number | null;
  donViId: number;
  kyBaoCaoCode: string;
  noiDungDaoTao: string;
  soTienSi: number;
  soThacSi: number;
  soDaiHoc: number;
  soCaoDang: number;
  soTrungCap: number;
  ghiChu: string;
}

type HocVienEditableField =
  | 'soTienSi'
  | 'soThacSi'
  | 'soDaiHoc'
  | 'soCaoDang'
  | 'soTrungCap'
  | 'ghiChu';

interface CellEditHistory {
  rowId: string;
  field: HocVienEditableField;
  oldValue: number | string | null;
  newValue: number | string | null;
  timestamp: number;
}

interface NangLucSoGridRow {
  localId: string;
  id: number | null;
  donViId: number;
  kyBaoCaoCode: string;
  nhomViTri: string;
  tongSoDienDanhGia: number;
  tongSoDat: number;
  tongSoChuaDat: number;
  ghiChu: string;
}

type NangLucSoEditableField =
  | 'tongSoDienDanhGia'
  | 'tongSoDat'
  | 'tongSoChuaDat'
  | 'ghiChu';

interface NangLucSoCellEditHistory {
  rowId: string;
  field: NangLucSoEditableField;
  oldValue: number | string | null;
  newValue: number | string | null;
  timestamp: number;
}

type ModuleViewMode = 'live' | 'history';

@Component({
  selector: 'app-business-modules-page',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    RouterLink,
    SectionCardComponent,
    FilterBarComponent,
    FormActionBarComponent,
    EmptyStateComponent,
    LoadingOverlayComponent,
    HasPermissionDirective,
    CalendarModule,
    DialogModule,
    DropdownModule,
    InputNumberModule,
    InputTextModule,
    InputTextareaModule,
    CheckboxModule,
    ButtonModule,
    TableModule,
    TooltipModule,
    PaginatorModule,
    AutoCompleteModule,
  ],
  templateUrl: './business-modules.page.html',
  styleUrl: './business-modules.page.scss',
})
export class BusinessModulesPage {
  readonly selectPanelStyleClass = APP_SELECT_PANEL_STYLE_CLASS;
  readonly dialogStyleClass = APP_DIALOG_STYLE_CLASS;
  readonly dialogContentStyleClass = APP_DIALOG_CONTENT_STYLE_CLASS;
  readonly dialogMaskStyleClass = APP_DIALOG_MASK_STYLE_CLASS;
  readonly tableStyleClass = APP_TABLE_STYLE_CLASS;
  readonly tableHeaderCellClass = APP_TABLE_HEADER_CELL_CLASS;
  readonly tableRowClass = APP_TABLE_ROW_CLASS;
  readonly tableBodyCellClass = APP_TABLE_BODY_CELL_CLASS;
  private readonly localDateFormatter = new Intl.DateTimeFormat('vi-VN', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  });
  readonly calendarLocaleVi = {
    firstDayOfWeek: 1,
    dayNames: [
      'Chủ nhật',
      'Thứ Hai',
      'Thứ Ba',
      'Thứ Tư',
      'Thứ Năm',
      'Thứ Sáu',
      'Thứ Bảy',
    ],
    dayNamesShort: ['CN', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7'],
    dayNamesMin: ['CN', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7'],
    monthNames: [
      'Tháng 1',
      'Tháng 2',
      'Tháng 3',
      'Tháng 4',
      'Tháng 5',
      'Tháng 6',
      'Tháng 7',
      'Tháng 8',
      'Tháng 9',
      'Tháng 10',
      'Tháng 11',
      'Tháng 12',
    ],
    monthNamesShort: [
      'T1',
      'T2',
      'T3',
      'T4',
      'T5',
      'T6',
      'T7',
      'T8',
      'T9',
      'T10',
      'T11',
      'T12',
    ],
    today: 'Hôm nay',
    clear: 'Xóa',
    weekHeader: 'Tuần',
  };

  readonly filter = this.formBuilder.group({
    kyCode: ['2026Q1'],
    viewMode: ['live' as ModuleViewMode],
  });

  readonly viewModeOptions: Array<{ label: string; value: ModuleViewMode }> = [
    { label: 'Live', value: 'live' },
    { label: 'Lịch sử', value: 'history' },
  ];

  form: UntypedFormGroup = this.formBuilder.group({});
  moduleConfig: BizModuleConfig = BIZ_MODULES['vanBanQppl'];

  items: BizRecordDto[] = [];
  selectedId: number | null = null;
  searchTerm = '';
  showFilterPanel = false;
  showCreatePanel = false;
  trainingTableFirst = 0;
  trainingPageSize = 10;
  readonly trainingPageSizeOptions = [
    { label: '10', value: 10 },
    { label: '20', value: 20 },
    { label: '50', value: 50 },
  ];
  readonly hinhThucOptions: Array<{ label: string; value: string | null }> = [
    { label: 'Trực tiếp', value: 'Trực tiếp' },
    { label: 'Trực tuyến', value: 'Trực tuyến' },
    { label: 'Kết hợp', value: 'Kết hợp' },
    { label: 'Tự học', value: 'Tự học' },
    { label: 'Khác', value: 'Khác' },
  ];

  readonly hinhThucFilterOptions: Array<{
    label: string;
    value: string | null;
  }> = [{ label: 'Tất cả', value: null }, ...this.hinhThucOptions];

  filterDraft = {
    donViToChuc: '',
    hinhThuc: null as string | null,
    thoiGianTu: null as Date | null,
    thoiGianDen: null as Date | null,
  };

  appliedFilters = {
    donViToChuc: '',
    hinhThuc: null as string | null,
    thoiGianTu: null as Date | null,
    thoiGianDen: null as Date | null,
  };

  loading = false;
  saving = false;
  submittingSnapshot = false;
  cancelingSnapshot = false;
  apiError = '';
  trainingDateRangeError = '';
  selectOptions: Record<string, CodeValueDto[]> = {};
  kyBaoCaos: KyBaoCaoDto[] = [];
  hocVienRows: HocVienGridRow[] = [];
  hocVienErrors: Record<string, string> = {};
  hocVienBaseline = '[]';
  hocVienContentSuggestions: string[] = [];
  hocVienSubmittedSnapshot: SnapshotDto | null = null;
  dirtyHocVienCells = new Set<string>();
  private readonly hocVienEditableFields: HocVienEditableField[] = [
    'soTienSi',
    'soThacSi',
    'soDaiHoc',
    'soCaoDang',
    'soTrungCap',
    'ghiChu',
  ];
  private readonly hocVienOriginalCellValues = new Map<
    string,
    number | string | null
  >();
  private readonly hocVienPendingCellEdits = new Map<
    string,
    number | string | null
  >();
  private hocVienUndoStack: CellEditHistory[] = [];
  private hocVienRowSeed = 0;
  nangLucSoRows: NangLucSoGridRow[] = [];
  nangLucSoErrors: Record<string, string> = {};
  nangLucSoBaseline = '[]';
  dirtyNangLucSoCells = new Set<string>();
  private readonly nangLucSoEditableFields: NangLucSoEditableField[] = [
    'tongSoDienDanhGia',
    'tongSoDat',
    'tongSoChuaDat',
    'ghiChu',
  ];
  private readonly nangLucSoOriginalCellValues = new Map<
    string,
    number | string | null
  >();
  private readonly nangLucSoPendingCellEdits = new Map<
    string,
    number | string | null
  >();
  private nangLucSoUndoStack: NangLucSoCellEditHistory[] = [];
  private nangLucSoRowSeed = 0;
  private readonly nangLucSoRequireExactAssessmentMatch = false;

  constructor(
    private readonly formBuilder: UntypedFormBuilder,
    private readonly route: ActivatedRoute,
    private readonly businessApi: BusinessModulesApi,
    private readonly codesApi: CodesApi,
    private readonly kyBaoCaoApi: KyBaoCaoApi,
    private readonly snapshotApi: SnapshotApi,
    public readonly authService: AuthService,
    private readonly donViModeService: DonViModeService,
    private readonly primeNgConfig: PrimeNGConfig,
    private readonly notificationService: NotificationService,
    private readonly confirmDialog: ConfirmDialogWrapperService,
  ) {
    this.primeNgConfig.setTranslation(this.calendarLocaleVi);
    this.initializeModule();
    this.buildForm();
    void this.donViModeService.ensureLoaded();
    void this.initializePage();
  }

  get isTongHopMode(): boolean {
    return this.donViModeService.isTongHop;
  }

  get fields(): BizFieldConfig[] {
    return this.moduleConfig.fields;
  }

  get isDaoTaoBoiDuongModule(): boolean {
    return this.moduleConfig.moduleKey === 'daoTaoBoiDuong';
  }

  get isDaoTaoHocVienModule(): boolean {
    return this.moduleConfig.moduleKey === 'daoTaoHocVien';
  }

  get isNangLucSoModule(): boolean {
    return this.moduleConfig.moduleKey === 'nangLucSo';
  }

  get isCustomModule(): boolean {
    return (
      this.isDaoTaoBoiDuongModule ||
      this.isDaoTaoHocVienModule ||
      this.isNangLucSoModule
    );
  }

  get customLoadingLabel(): string {
    if (this.isDaoTaoBoiDuongModule) {
      return 'Đang tải dữ liệu đào tạo bồi dưỡng...';
    }

    if (this.isNangLucSoModule) {
      return 'Đang tải dữ liệu năng lực số...';
    }

    return 'Đang tải dữ liệu đào tạo học viện...';
  }

  get hocVienPeriodOptions(): Array<{ label: string; value: string }> {
    return this.kyBaoCaos.map((item) => ({
      label: item.kyCode,
      value: item.kyCode,
    }));
  }

  get selectedKyBaoCao(): KyBaoCaoDto | null {
    const currentCode = String(this.filter.getRawValue().kyCode ?? '').trim();
    return this.kyBaoCaos.find((item) => item.kyCode === currentCode) ?? null;
  }

  get currentViewMode(): ModuleViewMode {
    return (this.filter.getRawValue().viewMode as ModuleViewMode) ?? 'live';
  }

  get isHistoryMode(): boolean {
    return this.currentViewMode === 'history';
  }

  get isHocVienReadonly(): boolean {
    return false;
  }

  get isHocVienSubmitted(): boolean {
    return !!this.hocVienSubmittedSnapshot;
  }

  get isHocVienDirty(): boolean {
    return this.isDaoTaoHocVienModule && this.dirtyHocVienCells.size > 0;
  }

  get hocVienDirtyCellCount(): number {
    return this.dirtyHocVienCells.size;
  }

  get isNangLucSoDirty(): boolean {
    return this.isNangLucSoModule && this.dirtyNangLucSoCells.size > 0;
  }

  get nangLucSoDirtyCellCount(): number {
    return this.dirtyNangLucSoCells.size;
  }

  get nlsTotalGroups(): number {
    return this.nangLucSoRows.length;
  }

  get nlsTotalAssessed(): number {
    return this.nangLucSoRows.reduce(
      (sum, item) => sum + this.sanitizeHocVienNumber(item.tongSoDienDanhGia),
      0,
    );
  }

  get nlsTotalPassed(): number {
    return this.nangLucSoRows.reduce(
      (sum, item) => sum + this.sanitizeHocVienNumber(item.tongSoDat),
      0,
    );
  }

  get nlsTotalNotPassed(): number {
    return this.nangLucSoRows.reduce(
      (sum, item) => sum + this.sanitizeHocVienNumber(item.tongSoChuaDat),
      0,
    );
  }

  get hocVienStatusLabel(): string {
    return this.isHocVienSubmitted ? 'Đã nộp' : 'Chưa nộp';
  }

  get hocVienStatusClass(): string {
    if (this.isHocVienSubmitted) {
      return 'training-status-pill--success';
    }

    return this.isHocVienDirty
      ? 'training-status-pill--warning'
      : 'training-status-pill--info';
  }

  get hocVienSubmitLabel(): string {
    return this.isHocVienSubmitted ? 'Nộp lại sau khi hủy' : 'Nộp báo cáo';
  }

  get hocVienSubmittedTimeText(): string {
    const rawValue =
      this.hocVienSubmittedSnapshot?.lockedAt ??
      this.hocVienSubmittedSnapshot?.submittedAt;
    return rawValue ? this.formatLocalDate(rawValue) : 'Chưa nộp';
  }

  get hvTotalRows(): number {
    return this.hocVienRows.length;
  }

  get hvTotalTienSi(): number {
    return this.hocVienRows.reduce(
      (sum, item) => sum + this.sanitizeHocVienNumber(item.soTienSi),
      0,
    );
  }

  get hvTotalThacSi(): number {
    return this.hocVienRows.reduce(
      (sum, item) => sum + this.sanitizeHocVienNumber(item.soThacSi),
      0,
    );
  }

  get hvTotalDaiHoc(): number {
    return this.hocVienRows.reduce(
      (sum, item) => sum + this.sanitizeHocVienNumber(item.soDaiHoc),
      0,
    );
  }

  get hvTotalCaoDang(): number {
    return this.hocVienRows.reduce(
      (sum, item) => sum + this.sanitizeHocVienNumber(item.soCaoDang),
      0,
    );
  }

  get hvTotalTrungCap(): number {
    return this.hocVienRows.reduce(
      (sum, item) => sum + this.sanitizeHocVienNumber(item.soTrungCap),
      0,
    );
  }

  get hvGrandTotal(): number {
    return this.hocVienRows.reduce(
      (sum, item) => sum + this.getHocVienRowTotal(item),
      0,
    );
  }

  get tableFields(): BizFieldConfig[] {
    return this.fields.filter((field) => field.showInTable);
  }

  get genericDisplayedItems(): BizRecordDto[] {
    return this.items;
  }

  get formFields(): BizFieldConfig[] {
    return this.fields.filter((field) => !this.shouldHideFieldInForm(field));
  }

  get filteredItems(): BizRecordDto[] {
    if (this.isDaoTaoHocVienModule) {
      return this.items;
    }

    if (!this.isDaoTaoBoiDuongModule) {
      return this.items;
    }

    return this.items.filter((item) => {
      const keyword = this.searchTerm.trim().toLowerCase();
      const tenKhoaHoc = String(item['tenKhoaHoc'] ?? '').toLowerCase();
      const donViToChuc = String(item['donViToChuc'] ?? '').toLowerCase();
      const hinhThuc = String(item['hinhThuc'] ?? '');

      const matchSearch = !keyword || tenKhoaHoc.includes(keyword);

      const donViFilter = this.appliedFilters.donViToChuc.trim().toLowerCase();
      const matchDonVi = !donViFilter || donViToChuc.includes(donViFilter);

      const matchHinhThuc =
        !this.appliedFilters.hinhThuc ||
        hinhThuc === this.appliedFilters.hinhThuc;

      const tu = this.parseDateValue(item['thoiGianTu']);
      const den = this.parseDateValue(item['thoiGianDen']);
      const fromFilter = this.parseDateValue(this.appliedFilters.thoiGianTu);
      const toFilter = this.parseDateValue(this.appliedFilters.thoiGianDen);

      const matchFrom = !fromFilter || (!!tu && tu >= fromFilter);
      const matchTo = !toFilter || (!!den && den <= toFilter);

      return matchSearch && matchDonVi && matchHinhThuc && matchFrom && matchTo;
    });
  }

  get totalCourses(): number {
    return this.items.length;
  }

  get totalStudents(): number {
    return this.items.reduce((sum, item) => {
      const value = Number(item['soLuongHv'] ?? 0);
      return sum + (Number.isFinite(value) ? value : 0);
    }, 0);
  }

  get totalHours(): number {
    return this.items.reduce((sum, item) => {
      const start = this.parseDateValue(item['thoiGianTu']);
      const end = this.parseDateValue(item['thoiGianDen']);
      if (!start || !end || end.getTime() < start.getTime()) {
        return sum;
      }

      const hours = (end.getTime() - start.getTime()) / (1000 * 60 * 60);
      return sum + Math.round(hours * 10) / 10;
    }, 0);
  }

  get requiresKyBaoCaoCode(): boolean {
    return this.moduleConfig.requiresKyBaoCaoCode !== false;
  }

  get currentDonViId(): number | null {
    return this.authService.profile()?.donViId ?? null;
  }

  get canManageCrossDonVi(): boolean {
    return this.authService.hasPermission('ky_bao_cao:approve');
  }

  canSaveCurrentMode(): boolean {
    return this.selectedId
      ? this.authService.hasPermission(this.moduleConfig.permissions.update)
      : this.authService.hasPermission(this.moduleConfig.permissions.create);
  }

  async load(force = false): Promise<void> {
    if (
      !force &&
      ((this.isDaoTaoHocVienModule &&
        this.hocVienRows.length > 0 &&
        this.isHocVienDirty) ||
        (this.isNangLucSoModule &&
          this.nangLucSoRows.length > 0 &&
          this.isNangLucSoDirty))
    ) {
      const confirmed = await this.confirmDialog.confirm({
        header: 'Tải lại dữ liệu',
        message:
          'Bạn đang có thay đổi chưa lưu. Tiếp tục tải dữ liệu sẽ bỏ các chỉnh sửa này.',
        acceptLabel: 'Tải lại',
        rejectLabel: 'Ở lại',
      });

      if (!confirmed) {
        return;
      }
    }

    this.loading = true;
    this.apiError = '';
    try {
      const rawKyCode = String(this.filter.getRawValue().kyCode ?? '').trim();
      const shouldUseKyCode =
        this.requiresKyBaoCaoCode ||
        (this.isCustomModule && this.isHistoryMode);
      const kyCode = shouldUseKyCode && rawKyCode ? rawKyCode : undefined;
      const items = await this.businessApi.getAll(
        this.moduleConfig.endpoint,
        kyCode,
        this.isCustomModule && this.isHistoryMode,
      );
      this.items = this.applyBusinessScope(items);

      if (this.isDaoTaoHocVienModule) {
        this.refreshHocVienRows(this.items, kyCode ?? '');
        await this.loadHocVienSnapshotStatus();
      }

      if (this.isNangLucSoModule) {
        this.refreshNangLucSoRows(this.items, kyCode ?? '');
      }
    } catch (error) {
      this.apiError = this.extractApiErrorMessage(error);
      this.items = [];
      if (this.isDaoTaoHocVienModule) {
        this.hocVienRows = [];
        this.hocVienBaseline = '[]';
        this.hocVienErrors = {};
        this.hocVienSubmittedSnapshot = null;
      }

      if (this.isNangLucSoModule) {
        this.nangLucSoRows = [];
        this.nangLucSoBaseline = '[]';
        this.nangLucSoErrors = {};
      }
    } finally {
      this.loading = false;
    }
  }

  async initializePage(): Promise<void> {
    await this.loadSelectOptions();
    if (!this.isDaoTaoBoiDuongModule) {
      await this.loadHocVienPeriods();
    }
    this.resetForm();
    await this.load(true);
  }

  async save(): Promise<void> {
    if (this.isHistoryMode && !this.isDaoTaoBoiDuongModule) {
      this.notificationService.show(
        'warning',
        'Chế độ lịch sử chỉ cho phép xem. Chuyển sang Live để chỉnh sửa.',
      );
      return;
    }

    this.trainingDateRangeError = '';
    if (
      this.isDaoTaoBoiDuongModule &&
      !this.validateTrainingDateRangeFromForm()
    ) {
      return;
    }

    if (this.form.invalid || this.saving || !this.canSaveCurrentMode()) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving = true;
    this.apiError = '';

    try {
      const payload = this.buildPayload();
      if (this.selectedId) {
        await this.businessApi.update(
          this.moduleConfig.endpoint,
          this.selectedId,
          payload,
        );
        this.notificationService.show(
          'success',
          'Cap nhat du lieu thanh cong.',
        );
      } else {
        await this.businessApi.create(this.moduleConfig.endpoint, payload);
        this.notificationService.show('success', 'Tao moi du lieu thanh cong.');
      }

      this.resetForm();
      await this.load();
      if (this.isCustomModule) {
        this.showCreatePanel = false;
      }
    } catch (error) {
      this.apiError = this.extractApiErrorMessage(error);
    } finally {
      this.saving = false;
    }
  }

  async select(item: BizRecordDto): Promise<void> {
    try {
      const detail = await this.businessApi.getById(
        this.moduleConfig.endpoint,
        item.id,
      );

      this.selectedId = detail.id;
      if (this.isCustomModule) {
        this.showCreatePanel = true;
      }

      const patch: Record<string, BizValue> = {};
      for (const field of this.fields) {
        patch[field.key] = (detail[field.key] ??
          this.getDefaultValue(field)) as BizValue;
      }
      this.form.patchValue(patch);
    } catch {
      this.notificationService.show('error', 'Khong the tai chi tiet ban ghi.');
    }
  }

  async remove(item: BizRecordDto): Promise<void> {
    if (!this.authService.hasPermission(this.moduleConfig.permissions.delete)) {
      return;
    }

    const confirmed = await this.confirmDialog.confirmDelete({
      message: `Xac nhan xoa ban ghi ${this.resolveItemLabel(item)}?`,
      acceptLabel: 'Xoa',
      rejectLabel: 'Huy',
    });

    if (!confirmed) {
      return;
    }

    await this.businessApi.delete(this.moduleConfig.endpoint, item.id);
    this.notificationService.show('success', 'Xoa du lieu thanh cong.');
    if (this.selectedId === item.id) {
      this.resetForm();
    }
    await this.load();
  }

  resetForm(): void {
    this.selectedId = null;
    const values: Record<string, BizValue> = {};
    const kyCode = String(this.filter.getRawValue().kyCode ?? '').trim();

    for (const field of this.fields) {
      if (field.key === 'donViId' && this.shouldBindDonViToAccount()) {
        values[field.key] = this.currentDonViId;
      } else if (field.key === 'kyBaoCaoCode' && this.requiresKyBaoCaoCode) {
        values[field.key] = kyCode;
      } else {
        values[field.key] = this.getDefaultValue(field);
      }
    }

    this.form.reset(values);
    this.trainingTableFirst = 0;
    this.trainingDateRangeError = '';
    if (this.isCustomModule) {
      this.showCreatePanel = false;
    }
  }

  displayValue(item: BizRecordDto, field: BizFieldConfig): string {
    const value = item[field.key];
    if (value === null || value === undefined || value === '') {
      return '-';
    }

    if (field.type === 'checkbox') {
      return value === true ? 'Co' : 'Khong';
    }

    if (field.type === 'select') {
      const option = this.selectOptions[field.key]?.find(
        (item) => item.value === value,
      );
      return option ? this.getCodeValueLabel(option) : String(value);
    }

    return String(value);
  }

  getSelectOptions(
    field: BizFieldConfig,
  ): Array<{ label: string; value: string | null }> {
    return (this.selectOptions[field.key] ?? []).map((option) => ({
      label: this.getCodeValueLabel(option),
      value: option.value,
    }));
  }

  isFieldInvalid(field: BizFieldConfig): boolean {
    const control = this.form.get(field.key);
    return !!control && control.invalid && control.touched;
  }

  private getCodeValueLabel(option: CodeValueDto): string {
    const name = String(option.name ?? '').trim();
    const description = String(option.description ?? '').trim();

    if (!description) {
      return name;
    }

    if (!name) {
      return description;
    }

    return `${name} - ${description}`;
  }

  getNangLucSoGroupTitle(value: string): string {
    return this.splitDisplayLabel(value).title;
  }

  getNangLucSoGroupDescription(value: string): string | null {
    return this.splitDisplayLabel(value).description;
  }

  private splitDisplayLabel(value: string | null | undefined): {
    title: string;
    description: string | null;
  } {
    const normalized = String(value ?? '').trim();
    if (!normalized) {
      return { title: '', description: null };
    }

    const separator = ' - ';
    const separatorIndex = normalized.indexOf(separator);
    if (separatorIndex < 0) {
      return { title: normalized, description: null };
    }

    const title = normalized.slice(0, separatorIndex).trim();
    const description = normalized
      .slice(separatorIndex + separator.length)
      .trim();

    return {
      title: title || normalized,
      description: description || null,
    };
  }

  getField(key: string): BizFieldConfig | null {
    return this.fields.find((field) => field.key === key) ?? null;
  }

  isRequired(key: string): boolean {
    return this.fields.some((field) => field.key === key && !!field.required);
  }

  onFieldBlur(key: string): void {
    this.form.get(key)?.markAsTouched();
  }

  isFieldInvalidByKey(key: string): boolean {
    const field = this.getField(key);
    return field ? this.isFieldInvalid(field) : false;
  }

  getFieldErrorMessage(key: string): string {
    if (key === 'thoiGianDen' && this.trainingDateRangeError) {
      return this.trainingDateRangeError;
    }

    const control = this.form.get(key);
    if (!control || !control.touched || !control.invalid) {
      return '';
    }

    if (control.hasError('required')) {
      switch (key) {
        case 'tenKhoaHoc':
          return 'Vui lòng nhập tên khóa học.';
        case 'donViToChuc':
          return 'Vui lòng nhập đơn vị tổ chức.';
        case 'noiDungDaoTao':
          return 'Vui lòng chọn nội dung đào tạo.';
        default:
          return 'Trường này là bắt buộc.';
      }
    }

    if (control.hasError('maxlength')) {
      const maxLength = this.getField(key)?.maxLength;
      return maxLength
        ? `Không được vượt quá ${maxLength} ký tự.`
        : 'Giá trị vượt quá độ dài cho phép.';
    }

    return 'Giá trị không hợp lệ.';
  }

  openTrainingCreateDialog(): void {
    if (this.isDaoTaoHocVienModule) {
      this.addHocVienRow();
      return;
    }

    if (this.isNangLucSoModule) {
      return;
    }

    this.resetForm();
    this.apiError = '';
    this.showCreatePanel = true;
  }

  closeTrainingDialog(): void {
    if (this.isDaoTaoHocVienModule || this.isNangLucSoModule) {
      this.apiError = '';
      return;
    }

    this.resetForm();
    this.apiError = '';
  }

  toggleFilterPanel(): void {
    this.showFilterPanel = !this.showFilterPanel;
  }

  applyTrainingFilters(): void {
    this.appliedFilters = {
      ...this.filterDraft,
      donViToChuc: this.filterDraft.donViToChuc.trim(),
    };
    this.trainingTableFirst = 0;
  }

  clearTrainingFilters(): void {
    this.filterDraft = {
      donViToChuc: '',
      hinhThuc: null,
      thoiGianTu: null,
      thoiGianDen: null,
    };
    this.appliedFilters = {
      ...this.filterDraft,
    };
    this.trainingTableFirst = 0;
  }

  rowNumber(indexOnPage: number, first = 0): number {
    return first + indexOnPage + 1;
  }

  get trainingCurrentPage(): number {
    return Math.floor(this.trainingTableFirst / this.trainingPageSize) + 1;
  }

  get trainingTotalPages(): number {
    return Math.max(
      1,
      Math.ceil(this.filteredItems.length / this.trainingPageSize),
    );
  }

  get trainingShowFrom(): number {
    return this.filteredItems.length === 0 ? 0 : this.trainingTableFirst + 1;
  }

  get trainingShowTo(): number {
    return Math.min(
      this.trainingTableFirst + this.trainingPageSize,
      this.filteredItems.length,
    );
  }

  get pagedTrainingItems(): BizRecordDto[] {
    return this.filteredItems.slice(
      this.trainingTableFirst,
      this.trainingTableFirst + this.trainingPageSize,
    );
  }

  onTrainingPageSizeChange(newSize: number): void {
    this.trainingPageSize = newSize;
    this.trainingTableFirst = 0;
  }

  onTrainingPageChange(event: any): void {
    this.trainingTableFirst = event.first ?? 0;
  }

  async selectTraining(item: BizRecordDto): Promise<void> {
    await this.select(item);
    this.showCreatePanel = true;
  }

  exportTrainingCsv(): void {
    const rows = this.filteredItems.map((item, index) => ({
      stt: index + 1,
      tenKhoaHoc: this.safeCell(item['tenKhoaHoc']),
      donViToChuc: this.safeCell(item['donViToChuc']),
      hinhThuc: this.safeCell(item['hinhThuc']),
      soLuongHv: this.safeCell(item['soLuongHv']),
      thoiGian: this.trainingTimeRange(item),
      ghiChu: this.safeCell(item['ghiChu']),
    }));

    const csvLines = [
      [
        'STT',
        'Tên khóa học',
        'Đơn vị tổ chức',
        'Hình thức',
        'Học viên',
        'Thời gian',
        'Ghi chú',
      ].join(','),
      ...rows.map((row) =>
        [
          row.stt,
          row.tenKhoaHoc,
          row.donViToChuc,
          row.hinhThuc,
          row.soLuongHv,
          row.thoiGian,
          row.ghiChu,
        ]
          .map((value) => `"${String(value).replace(/"/g, '""')}"`)
          .join(','),
      ),
    ];

    const blob = new Blob(['\uFEFF' + csvLines.join('\n')], {
      type: 'text/csv;charset=utf-8;',
    });

    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = `dao-tao-boi-duong-${new Date().toISOString().slice(0, 10)}.csv`;
    anchor.click();
    URL.revokeObjectURL(url);
  }

  exportHocVienCsv(): void {
    const rows = this.hocVienRows.map((item, index) => ({
      stt: index + 1,
      noiDungDaoTao: item.noiDungDaoTao || '-',
      soTienSi: item.soTienSi,
      soThacSi: item.soThacSi,
      soDaiHoc: item.soDaiHoc,
      soCaoDang: item.soCaoDang,
      soTrungCap: item.soTrungCap,
      tong: this.getHocVienRowTotal(item),
      ghiChu: item.ghiChu || '-',
    }));

    const csvLines = [
      [
        'STT',
        'Nội dung đào tạo',
        'Tiến sĩ',
        'Thạc sĩ',
        'Đại học',
        'Cao đẳng',
        'Trung cấp',
        'Ghi chú',
      ].join(','),
      ...rows.map((row) =>
        [
          row.stt,
          row.noiDungDaoTao,
          row.soTienSi,
          row.soThacSi,
          row.soDaiHoc,
          row.soCaoDang,
          row.soTrungCap,
          row.ghiChu,
        ]
          .map((value) => `"${String(value).replace(/"/g, '""')}"`)
          .join(','),
      ),
    ];

    const blob = new Blob(['\uFEFF' + csvLines.join('\n')], {
      type: 'text/csv;charset=utf-8;',
    });

    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = `dao-tao-hoc-vien-${new Date().toISOString().slice(0, 10)}.csv`;
    anchor.click();
    URL.revokeObjectURL(url);
  }

  trainingTimeRange(item: BizRecordDto): string {
    const tu = this.formatLocalDate(item['thoiGianTu']);
    const den = this.formatLocalDate(item['thoiGianDen']);
    if (tu === '-' && den === '-') {
      return '-';
    }

    return `${tu} - ${den}`;
  }

  formatDateCell(value: BizValue): string {
    return this.formatLocalDate(value);
  }

  periodStatusText(status?: number | null): string {
    switch (status) {
      case 1:
        return 'Chuẩn bị';
      case 2:
        return 'Đang mở';
      case 3:
        return 'Đã đóng';
      case 4:
        return 'Khóa';
      default:
        return 'Không xác định';
    }
  }

  addHocVienRow(): void {
    if (this.isHocVienReadonly || !this.canSaveCurrentMode()) {
      return;
    }

    const row = this.createHocVienRow();
    this.hocVienRows = [...this.hocVienRows, row];
    this.focusHocVienCell(this.hocVienRows.length - 1, 'noiDungDaoTao');
  }

  async removeHocVienRow(index: number): Promise<void> {
    if (this.isHocVienReadonly || !this.canSaveCurrentMode()) {
      return;
    }

    const row = this.hocVienRows[index];
    if (!row) {
      return;
    }

    this.hocVienRows = this.hocVienRows.filter(
      (_, currentIndex) => currentIndex !== index,
    );
    delete this.hocVienErrors[`${row.localId}:noiDungDaoTao`];
    delete this.hocVienErrors[`${row.localId}:soTienSi`];
    delete this.hocVienErrors[`${row.localId}:soThacSi`];
    delete this.hocVienErrors[`${row.localId}:soDaiHoc`];
    delete this.hocVienErrors[`${row.localId}:soCaoDang`];
    delete this.hocVienErrors[`${row.localId}:soTrungCap`];
  }

  onHocVienContentChange(row: HocVienGridRow, value: string): void {
    row.noiDungDaoTao = String(value ?? '');
    this.clearHocVienError(row, 'noiDungDaoTao');
  }

  searchHocVienContent(event: { query?: string | null }): void {
    const query = this.normalizeLookupText(event.query ?? '');
    const field = this.getField('noiDungDaoTao');
    const allValues = field
      ? Array.from(
          new Set(
            this.getSelectOptions(field)
              .flatMap((item) => [item.label, String(item.value ?? '')])
              .map((item) => item.trim())
              .filter(Boolean),
          ),
        )
      : [];

    this.hocVienContentSuggestions = query
      ? allValues
          .filter((item) => this.normalizeLookupText(item).includes(query))
          .slice(0, 12)
      : allValues.slice(0, 12);
  }

  onHocVienNumberChange(
    row: HocVienGridRow,
    field: 'soTienSi' | 'soThacSi' | 'soDaiHoc' | 'soCaoDang' | 'soTrungCap',
    value: unknown,
  ): void {
    row[field] = this.sanitizeHocVienNumber(value);
    this.clearHocVienError(row, field);
    this.updateHocVienDirtyCell(row, field);
  }

  onHocVienNoteChange(row: HocVienGridRow, value: string): void {
    row.ghiChu = String(value ?? '');
    this.updateHocVienDirtyCell(row, 'ghiChu');
  }

  getHocVienRowTotal(row: HocVienGridRow): number {
    return (
      this.sanitizeHocVienNumber(row.soTienSi) +
      this.sanitizeHocVienNumber(row.soThacSi) +
      this.sanitizeHocVienNumber(row.soDaiHoc) +
      this.sanitizeHocVienNumber(row.soCaoDang) +
      this.sanitizeHocVienNumber(row.soTrungCap)
    );
  }

  hasHocVienError(row: HocVienGridRow, field: string): boolean {
    return !!this.hocVienErrors[`${row.localId}:${field}`];
  }

  getHocVienError(row: HocVienGridRow, field: string): string {
    return this.hocVienErrors[`${row.localId}:${field}`] ?? '';
  }

  clearHocVienError(row: HocVienGridRow, field: string): void {
    delete this.hocVienErrors[`${row.localId}:${field}`];
  }

  onNangLucSoNumberChange(
    row: NangLucSoGridRow,
    field: 'tongSoDienDanhGia' | 'tongSoDat' | 'tongSoChuaDat',
    value: unknown,
  ): void {
    row[field] = this.sanitizeHocVienNumber(value);
    this.syncNangLucSoRowErrors(row);
    this.updateNangLucSoDirtyCell(row, field);
  }

  onNangLucSoNoteChange(row: NangLucSoGridRow, value: string): void {
    row.ghiChu = String(value ?? '');
    this.updateNangLucSoDirtyCell(row, 'ghiChu');
  }

  hasNangLucSoError(row: NangLucSoGridRow, field: string): boolean {
    return !!this.nangLucSoErrors[`${row.localId}:${field}`];
  }

  getNangLucSoError(row: NangLucSoGridRow, field: string): string {
    return this.nangLucSoErrors[`${row.localId}:${field}`] ?? '';
  }

  hasNangLucSoDirtyCell(
    row: NangLucSoGridRow,
    field: NangLucSoEditableField,
  ): boolean {
    return this.dirtyNangLucSoCells.has(
      this.getNangLucSoDirtyCellKey(row, field),
    );
  }

  captureNangLucSoCellFocus(
    row: NangLucSoGridRow,
    field: NangLucSoEditableField,
  ): void {
    const cellKey = this.getNangLucSoDirtyCellKey(row, field);
    if (!this.nangLucSoPendingCellEdits.has(cellKey)) {
      this.nangLucSoPendingCellEdits.set(
        cellKey,
        this.getNangLucSoComparableValue(field, row[field]),
      );
    }
  }

  commitNangLucSoCellEdit(
    row: NangLucSoGridRow,
    field: NangLucSoEditableField,
  ): void {
    const cellKey = this.getNangLucSoDirtyCellKey(row, field);
    const oldValue = this.nangLucSoPendingCellEdits.get(cellKey);
    this.nangLucSoPendingCellEdits.delete(cellKey);
    this.syncNangLucSoRowErrors(row);
    this.updateNangLucSoDirtyCell(row, field);

    if (oldValue === undefined) {
      return;
    }

    const newValue = this.getNangLucSoComparableValue(field, row[field]);
    if (oldValue === newValue) {
      return;
    }

    this.nangLucSoUndoStack.push({
      rowId: row.localId,
      field,
      oldValue,
      newValue,
      timestamp: Date.now(),
    });

    if (this.nangLucSoUndoStack.length > 100) {
      this.nangLucSoUndoStack.shift();
    }
  }

  handleNangLucSoEnter(
    event: Event,
    row: NangLucSoGridRow,
    rowIndex: number,
    field: NangLucSoEditableField,
  ): void {
    const keyboardEvent = event as KeyboardEvent;
    if (keyboardEvent.shiftKey) {
      return;
    }

    keyboardEvent.preventDefault();
    this.commitNangLucSoCellEdit(row, field);

    const nextIndex = Math.min(rowIndex + 1, this.nangLucSoRows.length - 1);
    this.focusNangLucSoCell(nextIndex, field);
  }

  hasHocVienDirtyCell(
    row: HocVienGridRow,
    field: HocVienEditableField,
  ): boolean {
    return this.dirtyHocVienCells.has(this.getHocVienDirtyCellKey(row, field));
  }

  captureHocVienCellFocus(
    row: HocVienGridRow,
    field: HocVienEditableField,
  ): void {
    const cellKey = this.getHocVienDirtyCellKey(row, field);
    if (!this.hocVienPendingCellEdits.has(cellKey)) {
      this.hocVienPendingCellEdits.set(
        cellKey,
        this.getHocVienComparableValue(field, row[field]),
      );
    }
  }

  commitHocVienCellEdit(
    row: HocVienGridRow,
    field: HocVienEditableField,
  ): void {
    const cellKey = this.getHocVienDirtyCellKey(row, field);
    const oldValue = this.hocVienPendingCellEdits.get(cellKey);
    this.hocVienPendingCellEdits.delete(cellKey);
    this.updateHocVienDirtyCell(row, field);

    if (oldValue === undefined) {
      return;
    }

    const newValue = this.getHocVienComparableValue(field, row[field]);
    if (oldValue === newValue) {
      return;
    }

    this.hocVienUndoStack.push({
      rowId: row.localId,
      field,
      oldValue,
      newValue,
      timestamp: Date.now(),
    });

    if (this.hocVienUndoStack.length > 100) {
      this.hocVienUndoStack.shift();
    }
  }

  @HostListener('document:keydown', ['$event'])
  onHocVienGridKeydown(event: KeyboardEvent): void {
    if (
      !(event.ctrlKey || event.metaKey) ||
      event.altKey ||
      event.shiftKey ||
      event.key.toLowerCase() !== 'z'
    ) {
      return;
    }

    const target = event.target as HTMLElement | null;

    if (this.isDaoTaoHocVienModule) {
      const activeCell = target
        ? this.resolveHocVienEditableCellFromElement(target)
        : null;

      if (
        activeCell &&
        this.hocVienPendingCellEdits.has(
          this.getHocVienDirtyCellKey(activeCell.row, activeCell.field),
        )
      ) {
        return;
      }

      if (this.hocVienUndoStack.length === 0) {
        return;
      }

      event.preventDefault();
      this.undoLastHocVienEdit();
      return;
    }

    if (!this.isNangLucSoModule) {
      return;
    }

    const activeCell = target
      ? this.resolveNangLucSoEditableCellFromElement(target)
      : null;

    if (
      activeCell &&
      this.nangLucSoPendingCellEdits.has(
        this.getNangLucSoDirtyCellKey(activeCell.row, activeCell.field),
      )
    ) {
      return;
    }

    if (this.nangLucSoUndoStack.length === 0) {
      return;
    }

    event.preventDefault();
    this.undoLastNangLucSoEdit();
  }

  handleHocVienEnter(
    event: Event,
    row: HocVienGridRow,
    rowIndex: number,
    field: HocVienEditableField,
  ): void {
    const keyboardEvent = event as KeyboardEvent;
    if (keyboardEvent.shiftKey) {
      return;
    }

    keyboardEvent.preventDefault();
    this.commitHocVienCellEdit(row, field);

    const nextIndex = Math.min(rowIndex + 1, this.hocVienRows.length - 1);
    this.focusHocVienCell(nextIndex, field);
  }

  onHocVienPaste(event: ClipboardEvent, startIndex: number): void {
    if (this.isHocVienReadonly) {
      return;
    }

    const text = event.clipboardData?.getData('text') ?? '';
    if (!text.includes('\t')) {
      return;
    }

    const lines = text
      .split(/\r?\n/)
      .map((line) => line.trim())
      .filter(Boolean);

    if (lines.length === 0) {
      return;
    }

    event.preventDefault();

    const rows = [...this.hocVienRows];
    while (rows.length < startIndex + lines.length) {
      rows.push(this.createHocVienRow());
    }

    lines.forEach((line, offset) => {
      const cells = line.split('\t');
      const row = rows[startIndex + offset];
      if (!row) {
        return;
      }

      row.noiDungDaoTao = String(cells[0] ?? '').trim();
      row.soTienSi = this.sanitizeHocVienNumber(cells[1]);
      row.soThacSi = this.sanitizeHocVienNumber(cells[2]);
      row.soDaiHoc = this.sanitizeHocVienNumber(cells[3]);

      if (cells.length >= 7) {
        row.soCaoDang = this.sanitizeHocVienNumber(cells[4]);
        row.soTrungCap = this.sanitizeHocVienNumber(cells[5]);
        row.ghiChu = String(cells[cells.length - 1] ?? '').trim();
      } else {
        row.ghiChu = String(cells[4] ?? row.ghiChu ?? '').trim();
      }
    });

    this.hocVienRows = rows;
  }

  async saveHocVienGrid(): Promise<void> {
    if (
      !this.isDaoTaoHocVienModule ||
      this.saving ||
      this.isHocVienReadonly ||
      !this.canSaveCurrentMode()
    ) {
      return;
    }

    const donViId = this.currentDonViId ?? 0;
    if (!donViId) {
      this.notificationService.show(
        'error',
        'Chưa xác định đơn vị để lưu dữ liệu.',
      );
      return;
    }

    this.hocVienRows = this.hocVienRows.filter(
      (row) => !this.isHocVienRowEmpty(row),
    );

    if (!this.validateHocVienRows()) {
      this.notificationService.show(
        'error',
        'Có dòng dữ liệu chưa hợp lệ. Vui lòng kiểm tra lại.',
      );
      return;
    }

    this.saving = true;
    this.apiError = '';

    try {
      const payload = {
        donViId,
        items: this.hocVienRows.map((row) => this.normalizeHocVienRow(row)),
      };

      const savedItems = await this.businessApi.saveMatrix(
        this.moduleConfig.endpoint,
        payload,
      );

      this.items = this.applyBusinessScope(savedItems);
      this.refreshHocVienRows(this.items, this.getCurrentHocVienKyCode());
      this.notificationService.show('success', 'Lưu dữ liệu thành công.');
    } catch (error) {
      this.apiError = this.extractApiErrorMessage(error);
      this.notificationService.show('error', this.apiError);
    } finally {
      this.saving = false;
    }
  }

  async saveNangLucSoGrid(): Promise<void> {
    if (!this.isNangLucSoModule || this.saving || !this.canSaveCurrentMode()) {
      return;
    }

    const donViId = this.currentDonViId ?? 0;
    if (!donViId) {
      this.notificationService.show(
        'error',
        'Chưa xác định đơn vị để lưu dữ liệu.',
      );
      return;
    }

    if (!this.validateNangLucSoRows()) {
      this.notificationService.show(
        'error',
        'Có ô dữ liệu chưa hợp lệ. Vui lòng kiểm tra lại.',
      );
      return;
    }

    this.saving = true;
    this.apiError = '';

    try {
      const payload = {
        donViId,
        items: this.nangLucSoRows.map((row) => this.normalizeNangLucSoRow(row)),
      };

      const savedItems = await this.businessApi.saveMatrix(
        this.moduleConfig.endpoint,
        payload,
      );

      this.items = this.applyBusinessScope(savedItems);
      this.refreshNangLucSoRows(this.items, this.getCurrentNangLucSoKyCode());
      this.notificationService.show('success', 'Lưu dữ liệu thành công.');
    } catch (error) {
      this.apiError = this.extractApiErrorMessage(error);
      this.notificationService.show('error', this.apiError);
    } finally {
      this.saving = false;
    }
  }

  async submitHocVienReport(): Promise<void> {
    if (
      !this.isDaoTaoHocVienModule ||
      this.submittingSnapshot ||
      this.isHocVienSubmitted ||
      !this.canSaveCurrentMode()
    ) {
      return;
    }

    const donViId = this.currentDonViId ?? 0;
    const selectedKy = this.selectedKyBaoCao;
    if (!donViId || !selectedKy) {
      this.notificationService.show(
        'error',
        'Vui lòng chọn kỳ báo cáo trước khi nộp.',
      );
      return;
    }

    const confirmed = await this.confirmDialog.confirm({
      header: 'Nộp báo cáo',
      message:
        'Dữ liệu hiện tại sẽ được nộp cho kỳ báo cáo đã chọn. Bạn vẫn có thể tiếp tục nhập liệu bình thường sau đó.',
      acceptLabel: 'Nộp báo cáo',
      rejectLabel: 'Hủy',
    });

    if (!confirmed) {
      return;
    }

    if (this.isHocVienDirty) {
      await this.saveHocVienGrid();
      if (this.isHocVienDirty || this.apiError) {
        return;
      }
    }

    this.submittingSnapshot = true;
    this.apiError = '';
    try {
      this.hocVienSubmittedSnapshot = await this.snapshotApi.submitCurrent({
        kyBaoCaoId: selectedKy.id,
        donViId,
        ghiChu: `Nộp báo cáo ${selectedKy.kyCode} từ màn hình đào tạo học viện`,
      });

      await this.loadHocVienSnapshotStatus();
      this.notificationService.show('success', 'Đã nộp báo cáo thành công.');
    } catch (error) {
      this.apiError = this.extractApiErrorMessage(error);
    } finally {
      this.submittingSnapshot = false;
    }
  }

  async cancelHocVienSubmission(): Promise<void> {
    if (
      !this.isDaoTaoHocVienModule ||
      this.cancelingSnapshot ||
      !this.hocVienSubmittedSnapshot ||
      !this.canSaveCurrentMode()
    ) {
      return;
    }

    const confirmed = await this.confirmDialog.confirm({
      header: 'Hủy nộp báo cáo',
      message: 'Bạn có chắc chắn muốn hủy báo cáo kỳ này không?',
      acceptLabel: 'Hủy nộp',
      rejectLabel: 'Đóng',
    });

    if (!confirmed) {
      return;
    }

    this.cancelingSnapshot = true;
    this.apiError = '';
    try {
      await this.snapshotApi.cancel(this.hocVienSubmittedSnapshot.id);
      this.hocVienSubmittedSnapshot = null;
      await this.loadHocVienSnapshotStatus();
      this.notificationService.show('success', 'Đã hủy nộp báo cáo.');
    } catch (error) {
      this.apiError = this.extractApiErrorMessage(error);
    } finally {
      this.cancelingSnapshot = false;
    }
  }

  async finalizeHocVienPeriod(): Promise<void> {
    if (
      !this.isDaoTaoHocVienModule ||
      this.saving ||
      !this.canSaveCurrentMode()
    ) {
      return;
    }

    const donViId = this.currentDonViId ?? 0;
    const kyBaoCaoCode = String(this.selectedKyBaoCao?.kyCode ?? '').trim();
    if (!donViId || !kyBaoCaoCode) {
      this.notificationService.show(
        'error',
        'Vui lòng chọn kỳ báo cáo hợp lệ để chốt kỳ.',
      );
      return;
    }

    if (this.isHocVienDirty) {
      await this.saveHocVienGrid();
      if (this.isHocVienDirty || this.apiError) {
        return;
      }
    }

    const confirmed = await this.confirmDialog.confirm({
      header: 'Chốt kỳ đào tạo học viện',
      message: `Xác nhận chốt dữ liệu kỳ ${kyBaoCaoCode} cho đơn vị hiện tại?`,
      acceptLabel: 'Chốt kỳ',
      rejectLabel: 'Hủy',
    });

    if (!confirmed) {
      return;
    }

    this.saving = true;
    this.apiError = '';
    try {
      await this.businessApi.finalizePeriod(this.moduleConfig.endpoint, {
        donViId,
        kyBaoCaoCode,
      });

      await this.load(true);
      await this.loadHocVienSnapshotStatus();
      this.notificationService.show(
        'success',
        `Đã chốt kỳ ${kyBaoCaoCode} thành công.`,
      );
    } catch (error) {
      this.apiError = this.extractApiErrorMessage(error);
      this.notificationService.show('error', this.apiError);
    } finally {
      this.saving = false;
    }
  }

  async finalizeNangLucSoPeriod(): Promise<void> {
    if (!this.isNangLucSoModule || this.saving || !this.canSaveCurrentMode()) {
      return;
    }

    const donViId = this.currentDonViId ?? 0;
    const kyBaoCaoCode = String(this.selectedKyBaoCao?.kyCode ?? '').trim();
    if (!donViId || !kyBaoCaoCode) {
      this.notificationService.show(
        'error',
        'Vui lòng chọn kỳ báo cáo hợp lệ để chốt kỳ.',
      );
      return;
    }

    if (this.isNangLucSoDirty) {
      await this.saveNangLucSoGrid();
      if (this.isNangLucSoDirty || this.apiError) {
        return;
      }
    }

    const confirmed = await this.confirmDialog.confirm({
      header: 'Chốt kỳ năng lực số',
      message: `Xác nhận chốt dữ liệu kỳ ${kyBaoCaoCode} cho đơn vị hiện tại?`,
      acceptLabel: 'Chốt kỳ',
      rejectLabel: 'Hủy',
    });

    if (!confirmed) {
      return;
    }

    this.saving = true;
    this.apiError = '';
    try {
      await this.businessApi.finalizePeriod(this.moduleConfig.endpoint, {
        donViId,
        kyBaoCaoCode,
      });

      await this.load(true);
      this.notificationService.show(
        'success',
        `Đã chốt kỳ ${kyBaoCaoCode} thành công.`,
      );
    } catch (error) {
      this.apiError = this.extractApiErrorMessage(error);
      this.notificationService.show('error', this.apiError);
    } finally {
      this.saving = false;
    }
  }

  async onCustomFilterChanged(): Promise<void> {
    await this.onFilterChanged();
  }

  async onFilterChanged(): Promise<void> {
    await this.load(true);
    if (this.isDaoTaoHocVienModule) {
      await this.loadHocVienSnapshotStatus();
    }
  }

  async finalizeCurrentModule(): Promise<void> {
    if (this.isCustomModule || this.saving || !this.canSaveCurrentMode()) {
      return;
    }

    const donViId = this.currentDonViId ?? 0;
    const kyBaoCaoCode = String(this.selectedKyBaoCao?.kyCode ?? '').trim();
    if (!donViId || !kyBaoCaoCode) {
      this.notificationService.show(
        'error',
        'Vui lòng chọn kỳ báo cáo hợp lệ để chốt kỳ.',
      );
      return;
    }

    const confirmed = await this.confirmDialog.confirm({
      header: `Chốt kỳ ${this.moduleConfig.title}`,
      message: `Xác nhận chốt dữ liệu kỳ ${kyBaoCaoCode} cho module hiện tại?`,
      acceptLabel: 'Chốt kỳ',
      rejectLabel: 'Hủy',
    });

    if (!confirmed) {
      return;
    }

    this.saving = true;
    this.apiError = '';
    try {
      await this.businessApi.finalizePeriod(this.moduleConfig.endpoint, {
        donViId,
        kyBaoCaoCode,
      });
      await this.load(true);
      this.notificationService.show(
        'success',
        `Đã chốt kỳ ${kyBaoCaoCode} thành công.`,
      );
    } catch (error) {
      this.apiError = this.extractApiErrorMessage(error);
      this.notificationService.show('error', this.apiError);
    } finally {
      this.saving = false;
    }
  }

  private async loadHocVienPeriods(): Promise<void> {
    try {
      const [currentKy, allKy] = await Promise.all([
        this.kyBaoCaoApi.getCurrent().catch(() => null),
        this.kyBaoCaoApi.getAll().catch(() => []),
      ]);

      this.kyBaoCaos = allKy;
      const currentCode = String(this.filter.getRawValue().kyCode ?? '').trim();
      const selectedCode =
        currentKy?.kyCode || allKy[0]?.kyCode || currentCode || '';

      this.filter.patchValue({ kyCode: selectedCode }, { emitEvent: false });
    } catch {
      this.kyBaoCaos = [];
      this.filter.patchValue({ kyCode: '' }, { emitEvent: false });
    }
  }

  private async loadHocVienSnapshotStatus(): Promise<void> {
    const selectedKy = this.selectedKyBaoCao;
    const donViId = this.currentDonViId;

    if (!selectedKy || !donViId) {
      this.hocVienSubmittedSnapshot = null;
      return;
    }

    try {
      const snapshots = await this.snapshotApi.getByKy(selectedKy.id);
      this.hocVienSubmittedSnapshot =
        snapshots
          .filter(
            (item) =>
              item.donViId === donViId &&
              (item.trangThai === 2 || item.trangThai === 3),
          )
          .sort((left, right) => right.phienBan - left.phienBan)[0] ?? null;
    } catch {
      this.hocVienSubmittedSnapshot = null;
    }
  }

  private refreshHocVienRows(
    items: BizRecordDto[],
    fallbackKyCode: string,
  ): void {
    const field = this.getField('noiDungDaoTao');
    const savedRows = items.map((item) => {
      const noiDungDisplay = field
        ? this.displayValue(item, field)
        : String(item['noiDungDaoTao'] ?? '');

      return {
        localId: `hv-${item.id}-${this.hocVienRowSeed++}`,
        id: item.id,
        donViId: Number(item.donViId ?? this.currentDonViId ?? 0),
        kyBaoCaoCode: String(item['kyBaoCaoCode'] ?? fallbackKyCode),
        noiDungDaoTao: noiDungDisplay === '-' ? '' : noiDungDisplay,
        soTienSi: this.sanitizeHocVienNumber(item['soTienSi']),
        soThacSi: this.sanitizeHocVienNumber(item['soThacSi']),
        soDaiHoc: this.sanitizeHocVienNumber(item['soDaiHoc']),
        soCaoDang: this.sanitizeHocVienNumber(item['soCaoDang']),
        soTrungCap: this.sanitizeHocVienNumber(item['soTrungCap']),
        ghiChu: String(item['ghiChu'] ?? ''),
      };
    });

    const masterContents = this.getHocVienMasterContents();
    if (masterContents.length === 0) {
      this.hocVienRows = savedRows;
    } else {
      const mergedRows = masterContents.map((content) => {
        const matched = savedRows.find(
          (row) =>
            this.normalizeLookupText(row.noiDungDaoTao) ===
              this.normalizeLookupText(content.label) ||
            this.normalizeLookupText(
              this.resolveHocVienContentValue(row.noiDungDaoTao),
            ) === this.normalizeLookupText(content.value),
        );

        return matched ?? this.createHocVienRow(content.label, fallbackKyCode);
      });

      const extraRows = savedRows.filter(
        (row) =>
          !masterContents.some(
            (content) =>
              this.normalizeLookupText(row.noiDungDaoTao) ===
                this.normalizeLookupText(content.label) ||
              this.normalizeLookupText(
                this.resolveHocVienContentValue(row.noiDungDaoTao),
              ) === this.normalizeLookupText(content.value),
          ),
      );

      this.hocVienRows = [...mergedRows, ...extraRows];
    }

    this.hocVienErrors = {};
    this.hocVienBaseline = this.serializeHocVienRows(this.hocVienRows);
    this.resetHocVienDirtyTracking();
  }

  private getHocVienMasterContents(): Array<{ label: string; value: string }> {
    const field = this.getField('noiDungDaoTao');
    if (!field) {
      return [];
    }

    const unique = new Map<string, { label: string; value: string }>();
    for (const option of this.getSelectOptions(field)) {
      const label = String(option.label ?? '').trim();
      const value = String(option.value ?? label).trim();
      const key = this.normalizeLookupText(value || label);
      if (!key) {
        continue;
      }

      unique.set(key, {
        label: label || value,
        value: value || label,
      });
    }

    return Array.from(unique.values());
  }

  private createHocVienRow(
    noiDungDaoTao = '',
    kyBaoCaoCode = this.getCurrentHocVienKyCode(),
  ): HocVienGridRow {
    return {
      localId: `hv-new-${this.hocVienRowSeed++}`,
      id: null,
      donViId: this.currentDonViId ?? 0,
      kyBaoCaoCode,
      noiDungDaoTao,
      soTienSi: 0,
      soThacSi: 0,
      soDaiHoc: 0,
      soCaoDang: 0,
      soTrungCap: 0,
      ghiChu: '',
    };
  }

  private getCurrentHocVienKyCode(): string {
    const rowKyCode = this.hocVienRows
      .map((row) => String(row.kyBaoCaoCode ?? '').trim())
      .find(Boolean);

    const selectedKyCode = String(this.selectedKyBaoCao?.kyCode ?? '').trim();
    const rawKyCode = String(this.filter.getRawValue().kyCode ?? '').trim();

    return rowKyCode || selectedKyCode || rawKyCode;
  }

  private serializeHocVienRows(rows: HocVienGridRow[]): string {
    return JSON.stringify(
      rows
        .filter((row) => !this.isHocVienRowEmpty(row))
        .map((row) => this.normalizeHocVienRow(row))
        .sort((a, b) =>
          String(a['noiDungDaoTao'] ?? '').localeCompare(
            String(b['noiDungDaoTao'] ?? ''),
            'vi',
          ),
        ),
    );
  }

  private normalizeHocVienRow(row: HocVienGridRow): Record<string, BizValue> {
    const kyBaoCaoCode = row.kyBaoCaoCode || this.getCurrentHocVienKyCode();
    const normalizedContent = this.resolveHocVienContentValue(
      row.noiDungDaoTao,
    );
    const nam = Number(kyBaoCaoCode.slice(0, 4)) || new Date().getFullYear();

    return {
      donViId: row.donViId || this.currentDonViId,
      nam,
      noiDungDaoTao: normalizedContent,
      soTienSi: this.sanitizeHocVienNumber(row.soTienSi),
      soThacSi: this.sanitizeHocVienNumber(row.soThacSi),
      soDaiHoc: this.sanitizeHocVienNumber(row.soDaiHoc),
      soCaoDang: this.sanitizeHocVienNumber(row.soCaoDang),
      soTrungCap: this.sanitizeHocVienNumber(row.soTrungCap),
      ghiChu: row.ghiChu.trim() || null,
    };
  }

  private isHocVienRowEmpty(row: HocVienGridRow): boolean {
    return (
      !row.noiDungDaoTao.trim() &&
      this.sanitizeHocVienNumber(row.soTienSi) === 0 &&
      this.sanitizeHocVienNumber(row.soThacSi) === 0 &&
      this.sanitizeHocVienNumber(row.soDaiHoc) === 0 &&
      this.sanitizeHocVienNumber(row.soCaoDang) === 0 &&
      this.sanitizeHocVienNumber(row.soTrungCap) === 0 &&
      !row.ghiChu.trim()
    );
  }

  private validateHocVienRows(): boolean {
    const nextErrors: Record<string, string> = {};

    for (const row of this.hocVienRows) {
      if (this.isHocVienRowEmpty(row)) {
        continue;
      }

      if (!row.noiDungDaoTao.trim()) {
        nextErrors[`${row.localId}:noiDungDaoTao`] =
          'Nội dung đào tạo là bắt buộc.';
      }

      for (const field of [
        'soTienSi',
        'soThacSi',
        'soDaiHoc',
        'soCaoDang',
        'soTrungCap',
      ] as const) {
        const value = Number(row[field]);
        if (!Number.isFinite(value) || value < 0) {
          nextErrors[`${row.localId}:${field}`] =
            'Giá trị phải là số nguyên không âm.';
        }
      }
    }

    this.hocVienErrors = nextErrors;
    return Object.keys(nextErrors).length === 0;
  }

  private resolveHocVienContentValue(rawValue: string): string {
    const normalized = this.normalizeLookupText(rawValue);
    if (!normalized) {
      return '';
    }

    const options = this.getSelectOptions(this.getField('noiDungDaoTao')!);
    const matched = options.find((item) => {
      const label = this.normalizeLookupText(item.label);
      const value = this.normalizeLookupText(item.value ?? '');
      return label === normalized || value === normalized;
    });

    return matched?.value ?? rawValue.trim();
  }

  private focusHocVienCell(
    rowIndex: number,
    field: string,
    allowCreate = false,
  ): void {
    if (allowCreate && rowIndex >= this.hocVienRows.length) {
      this.hocVienRows = [...this.hocVienRows, this.createHocVienRow()];
    }

    setTimeout(() => {
      const target = document.getElementById(
        this.getHocVienCellId(rowIndex, field),
      ) as HTMLInputElement | null;

      target?.focus();
      target?.select?.();
    });
  }

  getHocVienCellId(rowIndex: number, field: string): string {
    return `hoc-vien-${rowIndex}-${field}`;
  }

  private getHocVienDirtyCellKey(
    row: HocVienGridRow,
    field: HocVienEditableField,
  ): string {
    return `${row.localId}:${field}`;
  }

  private resolveHocVienEditableCellFromElement(
    target: HTMLElement,
  ): { row: HocVienGridRow; field: HocVienEditableField } | null {
    const inputId = target.id || target.closest('input')?.id || '';
    const match = /^hoc-vien-(\d+)-(.+)$/.exec(inputId);
    if (!match) {
      return null;
    }

    const rowIndex = Number(match[1]);
    const field = String(match[2]) as HocVienEditableField;
    const row = this.hocVienRows[rowIndex];
    if (!row || !this.hocVienEditableFields.includes(field)) {
      return null;
    }

    return { row, field };
  }

  private getHocVienComparableValue(
    field: HocVienEditableField,
    value: unknown,
  ): number | string | null {
    if (field === 'ghiChu') {
      return String(value ?? '').trim();
    }

    return this.sanitizeHocVienNumber(value);
  }

  private updateHocVienDirtyCell(
    row: HocVienGridRow,
    field: HocVienEditableField,
  ): void {
    const cellKey = this.getHocVienDirtyCellKey(row, field);
    const originalValue = this.hocVienOriginalCellValues.get(cellKey);
    const currentValue = this.getHocVienComparableValue(field, row[field]);

    if (originalValue === currentValue) {
      this.dirtyHocVienCells.delete(cellKey);
      return;
    }

    this.dirtyHocVienCells.add(cellKey);
  }

  private resetHocVienDirtyTracking(): void {
    this.dirtyHocVienCells = new Set<string>();
    this.hocVienOriginalCellValues.clear();
    this.hocVienPendingCellEdits.clear();
    this.hocVienUndoStack = [];

    for (const row of this.hocVienRows) {
      for (const field of this.hocVienEditableFields) {
        this.hocVienOriginalCellValues.set(
          this.getHocVienDirtyCellKey(row, field),
          this.getHocVienComparableValue(field, row[field]),
        );
      }
    }
  }

  private undoLastHocVienEdit(): void {
    const lastEdit = this.hocVienUndoStack.pop();
    if (!lastEdit) {
      return;
    }

    const row = this.hocVienRows.find(
      (item) => item.localId === lastEdit.rowId,
    );
    if (!row) {
      return;
    }

    if (lastEdit.field === 'ghiChu') {
      row.ghiChu = String(lastEdit.oldValue ?? '');
    } else {
      row[lastEdit.field] = this.sanitizeHocVienNumber(lastEdit.oldValue);
      this.clearHocVienError(row, lastEdit.field);
    }

    this.hocVienPendingCellEdits.delete(
      this.getHocVienDirtyCellKey(row, lastEdit.field),
    );
    this.updateHocVienDirtyCell(row, lastEdit.field);

    const rowIndex = this.hocVienRows.findIndex(
      (item) => item.localId === lastEdit.rowId,
    );
    if (rowIndex >= 0) {
      this.focusHocVienCell(rowIndex, lastEdit.field);
    }
  }

  private refreshNangLucSoRows(
    items: BizRecordDto[],
    fallbackKyCode: string,
  ): void {
    const field = this.getField('nhomViTri');
    const savedRows = items.map((item) => {
      const nhomDisplay = field
        ? this.displayValue(item, field)
        : String(item['nhomViTri'] ?? '');

      return {
        localId: `nls-${item.id}-${this.nangLucSoRowSeed++}`,
        id: item.id,
        donViId: Number(item.donViId ?? this.currentDonViId ?? 0),
        kyBaoCaoCode: String(item['kyBaoCaoCode'] ?? fallbackKyCode),
        nhomViTri: nhomDisplay === '-' ? '' : nhomDisplay,
        tongSoDienDanhGia: this.sanitizeHocVienNumber(
          item['tongSoDienDanhGia'],
        ),
        tongSoDat: this.sanitizeHocVienNumber(item['tongSoDat']),
        tongSoChuaDat: this.sanitizeHocVienNumber(item['tongSoChuaDat']),
        ghiChu: String(item['ghiChu'] ?? ''),
      } as NangLucSoGridRow;
    });

    const masterGroups = this.getNangLucSoMasterGroups();
    if (masterGroups.length === 0) {
      this.nangLucSoRows = savedRows;
    } else {
      const mergedRows = masterGroups.map((group) => {
        const matched = savedRows.find(
          (row) =>
            this.normalizeLookupText(row.nhomViTri) ===
              this.normalizeLookupText(group.label) ||
            this.normalizeLookupText(
              this.resolveNangLucSoGroupValue(row.nhomViTri),
            ) === this.normalizeLookupText(group.value),
        );

        return matched ?? this.createNangLucSoRow(group.label, fallbackKyCode);
      });

      const extraRows = savedRows.filter(
        (row) =>
          !masterGroups.some(
            (group) =>
              this.normalizeLookupText(row.nhomViTri) ===
                this.normalizeLookupText(group.label) ||
              this.normalizeLookupText(
                this.resolveNangLucSoGroupValue(row.nhomViTri),
              ) === this.normalizeLookupText(group.value),
          ),
      );

      this.nangLucSoRows = [...mergedRows, ...extraRows];
    }

    this.nangLucSoErrors = {};
    this.nangLucSoBaseline = JSON.stringify(
      this.nangLucSoRows
        .map((row) => this.normalizeNangLucSoRow(row))
        .sort((a, b) =>
          String(a['nhomViTri'] ?? '').localeCompare(
            String(b['nhomViTri'] ?? ''),
            'vi',
          ),
        ),
    );
    this.resetNangLucSoDirtyTracking();
  }

  private getNangLucSoMasterGroups(): Array<{ label: string; value: string }> {
    const field = this.getField('nhomViTri');
    if (!field) {
      return [];
    }

    const unique = new Map<string, { label: string; value: string }>();
    for (const option of this.getSelectOptions(field)) {
      const label = String(option.label ?? '').trim();
      const value = String(option.value ?? label).trim();
      const key = this.normalizeLookupText(value || label);
      if (!key) {
        continue;
      }

      unique.set(key, {
        label: label || value,
        value: value || label,
      });
    }

    return Array.from(unique.values());
  }

  private createNangLucSoRow(
    nhomViTri = '',
    kyBaoCaoCode = this.getCurrentNangLucSoKyCode(),
  ): NangLucSoGridRow {
    return {
      localId: `nls-new-${this.nangLucSoRowSeed++}`,
      id: null,
      donViId: this.currentDonViId ?? 0,
      kyBaoCaoCode,
      nhomViTri,
      tongSoDienDanhGia: 0,
      tongSoDat: 0,
      tongSoChuaDat: 0,
      ghiChu: '',
    };
  }

  private getCurrentNangLucSoKyCode(): string {
    const rowKyCode = this.nangLucSoRows
      .map((row) => String(row.kyBaoCaoCode ?? '').trim())
      .find(Boolean);

    const selectedKyCode = String(this.selectedKyBaoCao?.kyCode ?? '').trim();
    const rawKyCode = String(this.filter.getRawValue().kyCode ?? '').trim();

    return rowKyCode || selectedKyCode || rawKyCode;
  }

  getNangLucSoCellId(rowIndex: number, field: string): string {
    return `nang-luc-so-${rowIndex}-${field}`;
  }

  private focusNangLucSoCell(rowIndex: number, field: string): void {
    setTimeout(() => {
      const target = document.getElementById(
        this.getNangLucSoCellId(rowIndex, field),
      ) as HTMLInputElement | null;

      target?.focus();
      target?.select?.();
    });
  }

  private normalizeNangLucSoRow(
    row: NangLucSoGridRow,
  ): Record<string, BizValue> {
    return {
      donViId: row.donViId || this.currentDonViId,
      nhomViTri: this.resolveNangLucSoGroupValue(row.nhomViTri),
      tongSoDienDanhGia: this.sanitizeHocVienNumber(row.tongSoDienDanhGia),
      tongSoDat: this.sanitizeHocVienNumber(row.tongSoDat),
      tongSoChuaDat: this.sanitizeHocVienNumber(row.tongSoChuaDat),
      ghiChu: row.ghiChu.trim() || null,
    };
  }

  private validateNangLucSoRows(): boolean {
    const nextErrors: Record<string, string> = {};

    for (const row of this.nangLucSoRows) {
      if (!row.nhomViTri.trim()) {
        nextErrors[`${row.localId}:tongSoDienDanhGia`] =
          'Nhóm vị trí chưa được cấu hình.';
        continue;
      }

      const businessMessage = this.getNangLucSoValidationMessage(row);
      if (businessMessage) {
        nextErrors[`${row.localId}:tongSoDienDanhGia`] = businessMessage;
        nextErrors[`${row.localId}:tongSoDat`] = businessMessage;
        nextErrors[`${row.localId}:tongSoChuaDat`] = businessMessage;
      }
    }

    this.nangLucSoErrors = nextErrors;
    return Object.keys(nextErrors).length === 0;
  }

  private syncNangLucSoRowErrors(row: NangLucSoGridRow): void {
    delete this.nangLucSoErrors[`${row.localId}:tongSoDienDanhGia`];
    delete this.nangLucSoErrors[`${row.localId}:tongSoDat`];
    delete this.nangLucSoErrors[`${row.localId}:tongSoChuaDat`];

    const businessMessage = this.getNangLucSoValidationMessage(row);
    if (businessMessage) {
      this.nangLucSoErrors[`${row.localId}:tongSoDienDanhGia`] =
        businessMessage;
      this.nangLucSoErrors[`${row.localId}:tongSoDat`] = businessMessage;
      this.nangLucSoErrors[`${row.localId}:tongSoChuaDat`] = businessMessage;
    }
  }

  private getNangLucSoValidationMessage(row: NangLucSoGridRow): string | null {
    const assessed = this.sanitizeHocVienNumber(row.tongSoDienDanhGia);
    const passed = this.sanitizeHocVienNumber(row.tongSoDat);
    const notPassed = this.sanitizeHocVienNumber(row.tongSoChuaDat);
    const accounted = passed + notPassed;

    if (this.nangLucSoRequireExactAssessmentMatch) {
      return accounted === assessed
        ? null
        : 'Tổng số đạt và chưa đạt phải bằng tổng số diện đánh giá.';
    }

    return accounted <= assessed
      ? null
      : 'Tổng số đạt và chưa đạt không được vượt quá tổng số diện đánh giá.';
  }

  private resolveNangLucSoGroupValue(rawValue: string): string {
    const normalized = this.normalizeLookupText(rawValue);
    if (!normalized) {
      return '';
    }

    const field = this.getField('nhomViTri');
    if (!field) {
      return rawValue.trim();
    }

    const options = this.getSelectOptions(field);
    const matched = options.find((item) => {
      const label = this.normalizeLookupText(item.label);
      const value = this.normalizeLookupText(item.value ?? '');
      return label === normalized || value === normalized;
    });

    return matched?.value ?? rawValue.trim();
  }

  private getNangLucSoDirtyCellKey(
    row: NangLucSoGridRow,
    field: NangLucSoEditableField,
  ): string {
    return `${row.localId}:${field}`;
  }

  private resolveNangLucSoEditableCellFromElement(
    target: HTMLElement,
  ): { row: NangLucSoGridRow; field: NangLucSoEditableField } | null {
    const inputId = target.id || target.closest('input')?.id || '';
    const match = /^nang-luc-so-(\d+)-(.+)$/.exec(inputId);
    if (!match) {
      return null;
    }

    const rowIndex = Number(match[1]);
    const field = String(match[2]) as NangLucSoEditableField;
    const row = this.nangLucSoRows[rowIndex];
    if (!row || !this.nangLucSoEditableFields.includes(field)) {
      return null;
    }

    return { row, field };
  }

  private getNangLucSoComparableValue(
    field: NangLucSoEditableField,
    value: unknown,
  ): number | string | null {
    if (field === 'ghiChu') {
      return String(value ?? '').trim();
    }

    return this.sanitizeHocVienNumber(value);
  }

  private updateNangLucSoDirtyCell(
    row: NangLucSoGridRow,
    field: NangLucSoEditableField,
  ): void {
    const cellKey = this.getNangLucSoDirtyCellKey(row, field);
    const originalValue = this.nangLucSoOriginalCellValues.get(cellKey);
    const currentValue = this.getNangLucSoComparableValue(field, row[field]);

    if (originalValue === currentValue) {
      this.dirtyNangLucSoCells.delete(cellKey);
      return;
    }

    this.dirtyNangLucSoCells.add(cellKey);
  }

  private resetNangLucSoDirtyTracking(): void {
    this.dirtyNangLucSoCells = new Set<string>();
    this.nangLucSoOriginalCellValues.clear();
    this.nangLucSoPendingCellEdits.clear();
    this.nangLucSoUndoStack = [];

    for (const row of this.nangLucSoRows) {
      for (const field of this.nangLucSoEditableFields) {
        this.nangLucSoOriginalCellValues.set(
          this.getNangLucSoDirtyCellKey(row, field),
          this.getNangLucSoComparableValue(field, row[field]),
        );
      }
    }
  }

  private undoLastNangLucSoEdit(): void {
    const lastEdit = this.nangLucSoUndoStack.pop();
    if (!lastEdit) {
      return;
    }

    const row = this.nangLucSoRows.find(
      (item) => item.localId === lastEdit.rowId,
    );
    if (!row) {
      return;
    }

    if (lastEdit.field === 'ghiChu') {
      row.ghiChu = String(lastEdit.oldValue ?? '');
    } else {
      row[lastEdit.field] = this.sanitizeHocVienNumber(lastEdit.oldValue);
    }

    this.nangLucSoPendingCellEdits.delete(
      this.getNangLucSoDirtyCellKey(row, lastEdit.field),
    );
    this.syncNangLucSoRowErrors(row);
    this.updateNangLucSoDirtyCell(row, lastEdit.field);

    const rowIndex = this.nangLucSoRows.findIndex(
      (item) => item.localId === lastEdit.rowId,
    );
    if (rowIndex >= 0) {
      this.focusNangLucSoCell(rowIndex, lastEdit.field);
    }
  }

  private sanitizeHocVienNumber(value: unknown): number {
    const normalized = Number(value ?? 0);
    if (!Number.isFinite(normalized) || normalized < 0) {
      return 0;
    }

    return Math.trunc(normalized);
  }

  private normalizeLookupText(value: string | null | undefined): string {
    return String(value ?? '')
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .trim()
      .toLowerCase();
  }

  private initializeModule(): void {
    const moduleKey = String(
      this.route.snapshot.data['moduleKey'] ?? 'vanBanQppl',
    );
    this.moduleConfig = BIZ_MODULES[moduleKey] ?? BIZ_MODULES['vanBanQppl'];
  }

  private buildForm(): void {
    const controls: Record<string, unknown> = {};

    for (const field of this.fields) {
      const validators = [];
      if (field.required) {
        validators.push(Validators.required);
      }
      if (field.maxLength) {
        validators.push(Validators.maxLength(field.maxLength));
      }

      controls[field.key] = [this.getDefaultValue(field), validators];
    }

    this.form = this.formBuilder.group(controls);
    this.resetForm();
  }

  private getDefaultValue(field: BizFieldConfig): BizValue {
    if (field.defaultValue !== undefined) {
      return field.defaultValue;
    }

    if (field.type === 'checkbox') {
      return false;
    }

    if (field.type === 'select') {
      return null;
    }

    if (field.type === 'number' || field.type === 'decimal') {
      return null;
    }

    return '';
  }

  private buildPayload(): Record<string, BizValue> {
    const raw = this.form.getRawValue() as Record<string, unknown>;
    const payload: Record<string, BizValue> = {};

    for (const field of this.fields) {
      if (
        this.isCustomModule &&
        (field.key === 'donViId' || field.key === 'kyBaoCaoCode')
      ) {
        continue;
      }

      if (field.key === 'donViId' && this.shouldBindDonViToAccount()) {
        payload[field.key] = this.currentDonViId;
        continue;
      }

      if (field.key === 'kyBaoCaoCode' && this.requiresKyBaoCaoCode) {
        payload[field.key] = String(this.filter.getRawValue().kyCode ?? '');
        continue;
      }

      const value = raw[field.key];

      if (field.type === 'checkbox') {
        payload[field.key] = value === true;
        continue;
      }

      if (field.type === 'number' || field.type === 'decimal') {
        if (value === null || value === '') {
          payload[field.key] = field.required ? 0 : null;
        } else {
          payload[field.key] = Number(value);
        }
        continue;
      }

      if (field.type === 'date') {
        payload[field.key] = this.toApiDate(value);
        continue;
      }

      if (value === null || value === '') {
        payload[field.key] = field.required ? '' : null;
      } else {
        payload[field.key] = String(value);
      }
    }

    return payload;
  }

  private shouldHideFieldInForm(field: BizFieldConfig): boolean {
    if (field.key === 'kyBaoCaoCode' && this.requiresKyBaoCaoCode) {
      return true;
    }

    if (field.key === 'donViId' && this.shouldBindDonViToAccount()) {
      return true;
    }

    return false;
  }

  private shouldBindDonViToAccount(): boolean {
    return !!this.moduleConfig.bindDonViToAccount && !this.canManageCrossDonVi;
  }

  private applyBusinessScope(items: BizRecordDto[]): BizRecordDto[] {
    if (!this.shouldBindDonViToAccount() || !this.currentDonViId) {
      return items;
    }

    return items.filter((item) => item.donViId === this.currentDonViId);
  }

  private resolveItemLabel(item: BizRecordDto): string {
    for (const field of this.tableFields) {
      const value = item[field.key];
      if (value !== null && value !== undefined && value !== '') {
        return String(value);
      }
    }

    return `ID ${item.id}`;
  }

  private async loadSelectOptions(): Promise<void> {
    const selectFields = this.fields.filter(
      (field) => field.type === 'select' && field.codeKey,
    );

    if (selectFields.length === 0) {
      this.selectOptions = {};
      return;
    }

    const optionEntries = await Promise.all(
      selectFields.map(async (field) => {
        const code = await this.codesApi.getByCode(field.codeKey!);
        return [
          field.key,
          code.values.filter((value) => value.isActive),
        ] as const;
      }),
    );

    this.selectOptions = Object.fromEntries(optionEntries);
  }

  private parseDateValue(value: unknown): Date | null {
    if (!value) {
      return null;
    }

    if (value instanceof Date) {
      return Number.isNaN(value.getTime()) ? null : value;
    }

    const parsed = new Date(String(value));
    return Number.isNaN(parsed.getTime()) ? null : parsed;
  }

  private toApiDate(value: unknown): string | null {
    const parsed = this.parseDateValue(value);
    if (!parsed) {
      return null;
    }

    const year = parsed.getFullYear();
    const month = String(parsed.getMonth() + 1).padStart(2, '0');
    const day = String(parsed.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }

  private safeCell(value: BizValue): string {
    if (value === null || value === undefined || value === '') {
      return '-';
    }

    return String(value);
  }

  private formatLocalDate(value: BizValue): string {
    const parsed = this.parseDateValue(value);
    if (!parsed) {
      return '-';
    }

    return this.localDateFormatter.format(parsed);
  }

  private validateTrainingDateRangeFromForm(): boolean {
    const raw = this.form.getRawValue() as Record<string, unknown>;
    const fromDate = this.parseDateValue(raw['thoiGianTu']);
    const toDate = this.parseDateValue(raw['thoiGianDen']);

    if (!fromDate || !toDate) {
      return true;
    }

    if (toDate < fromDate) {
      this.trainingDateRangeError =
        'Thời gian đến phải lớn hơn hoặc bằng thời gian từ.';
      return false;
    }

    return true;
  }

  private extractApiErrorMessage(error: unknown): string {
    const fallback =
      'Không thể lưu dữ liệu. Vui lòng kiểm tra thông tin và thử lại.';

    if (error instanceof HttpErrorResponse) {
      const responseError = (error.error ?? {}) as
        | {
            message?: string;
            Message?: string;
            code?: string;
            Code?: string;
            errors?: Record<string, string[]>;
            Errors?: Record<string, string[]>;
            error?: {
              message?: string;
              Message?: string;
              code?: string;
              Code?: string;
            };
            Error?: {
              message?: string;
              Message?: string;
              code?: string;
              Code?: string;
            };
          }
        | undefined;

      const nestedError = responseError?.error ?? responseError?.Error;
      const message =
        nestedError?.message ??
        nestedError?.Message ??
        responseError?.message ??
        responseError?.Message;
      const code =
        nestedError?.code ??
        nestedError?.Code ??
        responseError?.code ??
        responseError?.Code;

      if (message) {
        return message;
      }

      if (code) {
        switch (code) {
          case 'KY_BAO_CAO_NOT_FOUND':
          case 'KY_NOT_FOUND':
            return 'Không tìm thấy kỳ báo cáo hợp lệ để lưu dữ liệu.';
          case 'KY_BAO_CAO_REQUIRED':
            return 'Thiếu kỳ báo cáo khi lưu dữ liệu.';
          default:
            return fallback;
        }
      }

      const validationErrors = responseError?.errors ?? responseError?.Errors;
      const firstValidation = validationErrors
        ? Object.values(validationErrors).flat().find(Boolean)
        : undefined;
      if (firstValidation) {
        return firstValidation;
      }

      if (error.message) {
        return error.message;
      }
    }

    return fallback;
  }
}
