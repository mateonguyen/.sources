import { CommonModule } from '@angular/common';
import { Component, computed, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { startWith } from 'rxjs';
import {
  FormBuilder,
  FormsModule,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { DialogModule } from 'primeng/dialog';
import { DropdownModule } from 'primeng/dropdown';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { InputTextareaModule } from 'primeng/inputtextarea';
import { TableModule } from 'primeng/table';
import { TooltipModule } from 'primeng/tooltip';
import { AuthService } from '../../core/auth/auth.service';
import { CodeDto, CodesApi, CodeValueDto } from '../codes/codes.api';
import {
  DonViApi,
  DonViDto,
  DonViParentCandidateDto,
} from '../don-vi/don-vi.api';
import { NotificationService } from '../../core/ui/notification.service';
import { ConfirmDialogWrapperService } from '../../shared/ui/confirm-dialog-wrapper.service';
import { FilterBarComponent } from '../../shared/ui/filter-bar.component';
import { LoadingOverlayComponent } from '../../shared/ui/loading-overlay.component';
import { SectionCardComponent } from '../../shared/ui/section-card.component';
import { TongHopModeBannerComponent } from '../../shared/ui/tong-hop-mode-banner.component';
import {
  APP_SELECT_PANEL_STYLE_CLASS,
  APP_TABLE_BODY_CELL_CLASS,
  APP_TABLE_HEADER_CELL_CLASS,
  APP_TABLE_ROW_CLASS,
  APP_TABLE_STYLE_CLASS,
} from '../../shared/ui/primeng-pt';
import {
  HeThongThongTinApi,
  HeThongThongTinDto,
  HtttTieuChuanDto,
  UpsertHeThongThongTinRequest,
  UpsertHtttTieuChuanRequest,
} from './he-thong-thong-tin.api';

type HtttTabKey = 'DUNG_CHUNG' | 'TU_PHAT_TRIEN' | 'HTTT_TIEU_CHUAN';

interface SelectOption<TValue extends string | number> {
  label: string;
  value: TValue;
}

interface HtttTieuChuanGridRow {
  localId: string;
  id: number | null;
  donViId: number;
  tenHeThong: string;
  dvt: string;
  soH05: number;
  soTinh: number;
  soXa: number;
  soDvTrucThuocBo: number;
  ghiChu: string;
  dirty: boolean;
  deleted?: boolean;
}

@Component({
  selector: 'app-he-thong-thong-tin-page',
  standalone: true,
  imports: [
    TongHopModeBannerComponent,
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    LoadingOverlayComponent,
    SectionCardComponent,
    DialogModule,
    DropdownModule,
    CheckboxModule,
    InputNumberModule,
    InputTextModule,
    InputTextareaModule,
    ButtonModule,
    TableModule,
    TooltipModule,
  ],
  templateUrl: './he-thong-thong-tin.page.html',
  styleUrl: './he-thong-thong-tin.page.scss',
})
export class HeThongThongTinPage {
  readonly selectPanelStyleClass = APP_SELECT_PANEL_STYLE_CLASS;
  readonly tableStyleClass = APP_TABLE_STYLE_CLASS;
  readonly tableHeaderCellClass = APP_TABLE_HEADER_CELL_CLASS;
  readonly tableRowClass = APP_TABLE_ROW_CLASS;
  readonly tableBodyCellClass = APP_TABLE_BODY_CELL_CLASS;

  readonly softwareForm = this.formBuilder.group({
    loaiPhanMem: ['DUNG_CHUNG', [Validators.required]],
    tenPhanMem: ['', [Validators.required]],
    donViPhatTrien: [''],
    donViQuanLy: ['', [Validators.required]],
    namTrienKhai: [null as number | null],
    phamViHoatDong: [''],
    phamViHoatDongKyThuat: [''],
    ungDungCnMoi: [''],
    khaNangTichHop: [''],
    daCongNhanSangKien: [false],
    ghiChu: [''],
  });

  readonly standardForm = this.formBuilder.group({
    tenHeThong: ['', [Validators.required]],
    dvt: [''],
    soH05: [0, [Validators.required]],
    soTinh: [0, [Validators.required]],
    soXa: [0, [Validators.required]],
    soDvTrucThuocBo: [0, [Validators.required]],
    ghiChu: [''],
  });

  loading = signal(false);
  savingSoftware = signal(false);
  savingStandard = signal(false);
  formDialogVisible = signal(false);
  standardDialogVisible = signal(false);
  filterTenPhanMem = signal('');
  activeTab = signal<HtttTabKey>('DUNG_CHUNG');

  softwareItems = signal<HeThongThongTinDto[]>([]);
  standardItems = signal<HtttTieuChuanDto[]>([]);
  standardCatalogValues = signal<CodeValueDto[]>([]);
  donViTree = signal<DonViDto[]>([]);
  donViFallbackOptions = signal<Array<SelectOption<string>>>([]);

  selectedSoftwareId = signal<number | null>(null);
  selectedStandardId = signal<number | null>(null);

  // ── Inline grid for HTTT tiêu chuẩn ───────────────────────────────────
  standardRows: HtttTieuChuanGridRow[] = [];
  standardBaseline = '';
  isStandardDirty = false;
  standardSaving = signal(false);
  private standardRowSeed = 0;

  readonly donViId = computed(
    () => this.authService.profile()?.donViId ?? null,
  );

  private readonly loaiPhanMemValue = toSignal(
    this.softwareForm.controls.loaiPhanMem.valueChanges.pipe(
      startWith(this.softwareForm.controls.loaiPhanMem.value),
    ),
    { initialValue: 'DUNG_CHUNG' as string | null },
  );

  readonly isTuPhatTrien = computed(
    () => this.loaiPhanMemValue() === 'TU_PHAT_TRIEN',
  );

  readonly isSoftwareTab = computed(
    () => this.activeTab() !== 'HTTT_TIEU_CHUAN',
  );

  readonly activeSectionTitle = computed(() => {
    const tab = this.activeTab();
    if (tab === 'DUNG_CHUNG') {
      return 'PM/CSDL Dung chung';
    }
    if (tab === 'TU_PHAT_TRIEN') {
      return 'PM/CSDL Tu phat trien';
    }
    return 'HTTT tieu chuan CAND';
  });

  readonly activeSectionDescription = computed(() => {
    const tab = this.activeTab();
    if (tab === 'DUNG_CHUNG') {
      return 'Cuc nghiep vu trien khai dung chung.';
    }
    if (tab === 'TU_PHAT_TRIEN') {
      return 'Don vi dia phuong nghien cuu va phat trien.';
    }
    return 'Thong ke so luong trien khai theo cap don vi.';
  });

  readonly dungChungItems = computed(() => {
    const filter = this.filterTenPhanMem().toLowerCase().trim();
    return this.softwareItems()
      .filter((item) => item.loaiPhanMem === 'DUNG_CHUNG')
      .filter(
        (item) => !filter || item.tenPhanMem.toLowerCase().includes(filter),
      );
  });

  readonly tuPhatTrienItems = computed(() => {
    const filter = this.filterTenPhanMem().toLowerCase().trim();
    return this.softwareItems()
      .filter((item) => item.loaiPhanMem === 'TU_PHAT_TRIEN')
      .filter(
        (item) => !filter || item.tenPhanMem.toLowerCase().includes(filter),
      );
  });

  readonly dungChungCount = computed(
    () =>
      this.softwareItems().filter((item) => item.loaiPhanMem === 'DUNG_CHUNG')
        .length,
  );

  readonly tuPhatTrienCount = computed(
    () =>
      this.softwareItems().filter(
        (item) => item.loaiPhanMem === 'TU_PHAT_TRIEN',
      ).length,
  );

  readonly standardCount = computed(() => this.standardItems().length);

  readonly donViQuanLyOptions = computed<Array<SelectOption<string>>>(() =>
    this.flattenDonViOptions(this.donViTree()).length > 0
      ? this.flattenDonViOptions(this.donViTree())
      : this.donViFallbackOptions(),
  );

  readonly standardCatalogOptions = computed<Array<SelectOption<string>>>(() =>
    this.standardCatalogValues().map((item) => ({
      label: item.name,
      value: item.value,
    })),
  );

  constructor(
    private readonly formBuilder: FormBuilder,
    private readonly authService: AuthService,
    private readonly heThongApi: HeThongThongTinApi,
    private readonly codesApi: CodesApi,
    private readonly donViApi: DonViApi,
    private readonly notificationService: NotificationService,
    private readonly confirmDialog: ConfirmDialogWrapperService,
  ) {
    this.softwareForm.controls.loaiPhanMem.valueChanges.subscribe((value) => {
      if (value !== 'TU_PHAT_TRIEN') {
        this.softwareForm.patchValue(
          { donViPhatTrien: '', daCongNhanSangKien: false },
          { emitEvent: false },
        );
      }
    });

    void this.initialize();
  }

  async initialize(): Promise<void> {
    this.loading.set(true);
    try {
      const [
        softwareItems,
        standardItems,
        standardCode,
        donViTree,
        donViParents,
      ] = await Promise.all([
        this.heThongApi.getAll().catch(() => []),
        this.heThongApi.getAllTieuChuan().catch(() => []),
        this.codesApi
          .getByCode('HTTT_TIEU_CHUAN_CAND')
          .catch(() => null as CodeDto | null),
        this.donViApi.getTree().catch(() => []),
        this.donViApi.getParentCandidates().catch(() => []),
      ]);

      this.softwareItems.set(softwareItems);
      this.standardItems.set(standardItems);
      const standardCatalogValues =
        standardCode && standardCode.values.length > 0
          ? standardCode.values
          : this.getDefaultStandardCatalogValues();
      this.standardCatalogValues.set(standardCatalogValues);
      const scopedDonViTree = this.resolveUserDonViSubtree(donViTree);
      this.donViTree.set(scopedDonViTree);
      this.donViFallbackOptions.set(
        this.mapDonViParentCandidatesToOptions(donViParents),
      );
      if (scopedDonViTree.length === 0) {
        this.notificationService.show(
          'warning',
          'Không lấy được cây đơn vị; đã chuyển sang danh sách đơn vị dạng phẳng.',
        );
      }
      this.refreshStandardRows(standardItems, standardCatalogValues);
    } finally {
      this.loading.set(false);
    }
  }

  async load(): Promise<void> {
    this.loading.set(true);
    try {
      const [softwareItems, standardItems] = await Promise.all([
        this.heThongApi.getAll().catch(() => []),
        this.heThongApi.getAllTieuChuan().catch(() => []),
      ]);
      this.softwareItems.set(softwareItems);
      this.standardItems.set(standardItems);
      const catalogValues =
        this.standardCatalogValues().length > 0
          ? this.standardCatalogValues()
          : this.getDefaultStandardCatalogValues();
      this.standardCatalogValues.set(catalogValues);
      this.refreshStandardRows(standardItems, catalogValues);
    } finally {
      this.loading.set(false);
    }
  }

  openSoftwareCreateDialog(loaiPhanMem: string = 'DUNG_CHUNG'): void {
    this.resetSoftwareForm();
    this.softwareForm.patchValue({ loaiPhanMem });
    this.formDialogVisible.set(true);
  }

  setActiveTab(tab: HtttTabKey): void {
    this.activeTab.set(tab);
  }

  openCreateForActiveTab(): void {
    const tab = this.activeTab();
    if (tab === 'HTTT_TIEU_CHUAN') {
      this.openStandardCreateDialog();
      return;
    }

    this.openSoftwareCreateDialog(tab);
  }

  openSoftwareEditDialog(item: HeThongThongTinDto): void {
    this.selectedSoftwareId.set(item.id);
    this.softwareForm.patchValue({
      loaiPhanMem: item.loaiPhanMem,
      tenPhanMem: item.tenPhanMem,
      donViPhatTrien: item.donViPhatTrien ?? '',
      donViQuanLy: item.donViQuanLy ?? '',
      namTrienKhai: item.namTrienKhai ?? null,
      phamViHoatDong: item.phamViHoatDong ?? '',
      phamViHoatDongKyThuat: item.phamViHoatDongKyThuat ?? '',
      ungDungCnMoi:
        typeof item.ungDungCnMoi === 'string' ? item.ungDungCnMoi : '',
      khaNangTichHop:
        typeof item.khaNangTichHop === 'string' ? item.khaNangTichHop : '',
      daCongNhanSangKien: item.daCongNhanSangKien,
      ghiChu: item.ghiChu ?? '',
    });
    this.formDialogVisible.set(true);
  }

  closeSoftwareDialog(): void {
    this.formDialogVisible.set(false);
    this.resetSoftwareForm();
  }

  openStandardCreateDialog(): void {
    this.resetStandardForm();
    this.standardDialogVisible.set(true);
  }

  openStandardEditDialog(item: HtttTieuChuanDto): void {
    this.selectedStandardId.set(item.id);
    this.standardForm.patchValue({
      tenHeThong: item.tenHeThong,
      dvt: item.dvt ?? '',
      soH05: item.soH05,
      soTinh: item.soTinh,
      soXa: item.soXa,
      soDvTrucThuocBo: item.soDvTrucThuocBo,
      ghiChu: item.ghiChu ?? '',
    });
    this.standardDialogVisible.set(true);
  }

  closeStandardDialog(): void {
    this.standardDialogVisible.set(false);
    this.resetStandardForm();
  }

  async saveSoftware(): Promise<void> {
    if (this.softwareForm.invalid || this.savingSoftware()) {
      this.softwareForm.markAllAsTouched();
      return;
    }

    const donViId = this.donViId();
    if (donViId === null) return;

    this.savingSoftware.set(true);
    try {
      const raw = this.softwareForm.getRawValue();
      const payload: UpsertHeThongThongTinRequest = {
        donViId,
        loaiPhanMem: String(raw.loaiPhanMem ?? 'DUNG_CHUNG'),
        tenPhanMem: String(raw.tenPhanMem ?? '').trim(),
        donViPhatTrien: this.isTuPhatTrien()
          ? this.normalizeText(raw.donViPhatTrien)
          : null,
        donViQuanLy: this.normalizeText(raw.donViQuanLy),
        namTrienKhai:
          raw.namTrienKhai === null || raw.namTrienKhai === undefined
            ? null
            : Number(raw.namTrienKhai),
        phamViHoatDong: this.normalizeText(raw.phamViHoatDong),
        phamViHoatDongKyThuat: this.normalizeText(raw.phamViHoatDongKyThuat),
        ungDungCnMoi: this.normalizeText(raw.ungDungCnMoi),
        khaNangTichHop: this.normalizeText(raw.khaNangTichHop),
        daCongNhanSangKien:
          this.isTuPhatTrien() && raw.daCongNhanSangKien === true,
        ghiChu: this.normalizeText(raw.ghiChu),
      };

      if (this.selectedSoftwareId()) {
        await this.heThongApi.update(this.selectedSoftwareId()!, payload);
        this.notificationService.show(
          'success',
          'Cập nhật hệ thống thông tin thành công.',
        );
      } else {
        await this.heThongApi.create(payload);
        this.notificationService.show(
          'success',
          'Thêm mới hệ thống thông tin thành công.',
        );
      }

      this.closeSoftwareDialog();
      await this.load();
    } finally {
      this.savingSoftware.set(false);
    }
  }

  async saveStandard(): Promise<void> {
    if (this.standardForm.invalid || this.savingStandard()) {
      this.standardForm.markAllAsTouched();
      return;
    }

    const donViId = this.donViId();
    if (donViId === null) return;

    this.savingStandard.set(true);
    try {
      const raw = this.standardForm.getRawValue();
      const payload: UpsertHtttTieuChuanRequest = {
        donViId,
        tenHeThong: String(raw.tenHeThong ?? '').trim(),
        dvt: this.normalizeText(raw.dvt),
        soH05: Number(raw.soH05 ?? 0),
        soTinh: Number(raw.soTinh ?? 0),
        soXa: Number(raw.soXa ?? 0),
        soDvTrucThuocBo: Number(raw.soDvTrucThuocBo ?? 0),
        ghiChu: this.normalizeText(raw.ghiChu),
      };

      if (this.selectedStandardId()) {
        await this.heThongApi.updateTieuChuan(
          this.selectedStandardId()!,
          payload,
        );
        this.notificationService.show(
          'success',
          'Cập nhật HTTT tiêu chuẩn thành công.',
        );
      } else {
        await this.heThongApi.createTieuChuan(payload);
        this.notificationService.show(
          'success',
          'Thêm mới HTTT tiêu chuẩn thành công.',
        );
      }

      this.closeStandardDialog();
      await this.load();
    } finally {
      this.savingStandard.set(false);
    }
  }

  async removeSoftware(item: HeThongThongTinDto): Promise<void> {
    const confirmed = await this.confirmDialog.confirmDelete({
      message: `Xác nhận xóa phần mềm "${item.tenPhanMem}"?`,
      acceptLabel: 'Xóa',
      rejectLabel: 'Hủy',
    });
    if (!confirmed) return;

    await this.heThongApi.delete(item.id);
    if (this.selectedSoftwareId() === item.id) {
      this.resetSoftwareForm();
    }
    this.notificationService.show('success', 'Xóa phần mềm thành công.');
    await this.load();
  }

  async removeStandard(item: HtttTieuChuanDto): Promise<void> {
    const confirmed = await this.confirmDialog.confirmDelete({
      message: `Xác nhận xóa "${item.tenHeThong}"?`,
      acceptLabel: 'Xóa',
      rejectLabel: 'Hủy',
    });
    if (!confirmed) return;

    await this.heThongApi.deleteTieuChuan(item.id);
    if (this.selectedStandardId() === item.id) {
      this.resetStandardForm();
    }
    this.notificationService.show('success', 'Xóa HTTT tiêu chuẩn thành công.');
    await this.load();
  }

  resetSoftwareForm(): void {
    this.selectedSoftwareId.set(null);
    this.softwareForm.reset({
      loaiPhanMem:
        this.activeTab() === 'HTTT_TIEU_CHUAN'
          ? 'DUNG_CHUNG'
          : this.activeTab(),
      tenPhanMem: '',
      donViPhatTrien: '',
      donViQuanLy: '',
      namTrienKhai: null,
      phamViHoatDong: '',
      phamViHoatDongKyThuat: '',
      ungDungCnMoi: '',
      khaNangTichHop: '',
      daCongNhanSangKien: false,
      ghiChu: '',
    });
  }

  resetStandardForm(): void {
    this.selectedStandardId.set(null);
    this.standardForm.reset({
      tenHeThong: '',
      dvt: '',
      soH05: 0,
      soTinh: 0,
      soXa: 0,
      soDvTrucThuocBo: 0,
      ghiChu: '',
    });
  }

  standardTotal(
    field: 'soH05' | 'soTinh' | 'soXa' | 'soDvTrucThuocBo',
  ): number {
    return this.standardRows.reduce((sum, row) => sum + (row[field] ?? 0), 0);
  }

  resolveBool(value: boolean): string {
    return value ? 'Có' : 'Không';
  }

  // ── HTTT Tiêu chuẩn inline grid ──────────────────────────────────────────

  private refreshStandardRows(
    items: HtttTieuChuanDto[],
    _catalog: CodeValueDto[],
  ): void {
    this.standardRows = items.map(
      (item) =>
        ({
          localId: `httt-std-${item.id}-${this.standardRowSeed++}`,
          id: item.id,
          donViId: item.donViId,
          tenHeThong: item.tenHeThong,
          dvt: item.dvt ?? '',
          soH05: item.soH05,
          soTinh: item.soTinh,
          soXa: item.soXa,
          soDvTrucThuocBo: item.soDvTrucThuocBo,
          ghiChu: item.ghiChu ?? '',
          dirty: false,
          deleted: false,
        }) as HtttTieuChuanGridRow,
    );
    this.standardBaseline = JSON.stringify(
      this.standardRows.map((r) => this.normalizeStandardRow(r)),
    );
    this.isStandardDirty = false;
  }

  private normalizeStandardRow(row: HtttTieuChuanGridRow) {
    return {
      tenHeThong: row.tenHeThong,
      dvt: row.dvt,
      soH05: row.soH05,
      soTinh: row.soTinh,
      soXa: row.soXa,
      soDvTrucThuocBo: row.soDvTrucThuocBo,
      ghiChu: row.ghiChu,
    };
  }

  onStandardNumberChange(
    row: HtttTieuChuanGridRow,
    field: keyof Pick<
      HtttTieuChuanGridRow,
      'soH05' | 'soTinh' | 'soXa' | 'soDvTrucThuocBo'
    >,
    value: number | null,
  ): void {
    row[field] = value ?? 0;
    row.dirty = true;
    this.isStandardDirty = true;
  }

  onStandardTextChange(
    row: HtttTieuChuanGridRow,
    field: keyof Pick<HtttTieuChuanGridRow, 'tenHeThong' | 'dvt' | 'ghiChu'>,
    value: string,
  ): void {
    row[field] = value;
    row.dirty = true;
    this.isStandardDirty = true;
  }

  isStandardRequiredMissing(
    row: HtttTieuChuanGridRow,
    field: keyof Pick<HtttTieuChuanGridRow, 'tenHeThong'>,
  ): boolean {
    return String(row[field] ?? '').trim().length === 0;
  }

  addStandardRow(): void {
    const donViId = this.donViId() ?? 0;
    this.standardRows = [
      ...this.standardRows,
      {
        localId: `httt-std-new-${this.standardRowSeed++}`,
        id: null,
        donViId,
        tenHeThong: '',
        dvt: '',
        soH05: 0,
        soTinh: 0,
        soXa: 0,
        soDvTrucThuocBo: 0,
        ghiChu: '',
        dirty: true,
        deleted: false,
      },
    ];
    this.isStandardDirty = true;
  }

  deleteStandardRow(row: HtttTieuChuanGridRow, rowIndex: number): void {
    if (row.id === null) {
      // New row not yet saved - just remove it
      this.standardRows = this.standardRows.filter((_, i) => i !== rowIndex);
    } else {
      // Existing row - mark as deleted
      row.deleted = true;
      row.dirty = true;
    }
    this.isStandardDirty = true;
  }

  getDisplayableStandardRows(): HtttTieuChuanGridRow[] {
    return this.standardRows.filter((row) => !row.deleted);
  }

  async saveStandardGrid(): Promise<void> {
    if (this.standardSaving()) return;
    const donViId = this.donViId();
    if (donViId === null) return;

    const dirtyRows = this.standardRows.filter(
      (row) => row.dirty && !row.deleted,
    );
    const deletedRows = this.standardRows.filter(
      (row) => row.deleted && row.id !== null,
    );

    if (dirtyRows.length === 0 && deletedRows.length === 0) {
      this.notificationService.show('info', 'Chưa có thay đổi nào để lưu.');
      return;
    }

    const invalidRows = dirtyRows.filter(
      (row) => row.tenHeThong.trim().length === 0,
    );
    if (invalidRows.length > 0) {
      this.notificationService.show(
        'warning',
        'Tên hệ thống không được để trống.',
      );
      return;
    }

    this.standardSaving.set(true);
    try {
      // Save/update non-deleted dirty rows
      const savePromises = dirtyRows.map((row) => {
        const payload: UpsertHtttTieuChuanRequest = {
          donViId,
          tenHeThong: row.tenHeThong.trim(),
          dvt: row.dvt?.trim() || null,
          soH05: row.soH05,
          soTinh: row.soTinh,
          soXa: row.soXa,
          soDvTrucThuocBo: row.soDvTrucThuocBo,
          ghiChu: row.ghiChu?.trim() || null,
        };
        if (row.id !== null) {
          return this.heThongApi
            .updateTieuChuan(row.id, payload)
            .then((dto) => {
              row.id = dto.id;
              row.dirty = false;
            });
        } else {
          return this.heThongApi.createTieuChuan(payload).then((dto) => {
            row.id = dto.id;
            row.dirty = false;
          });
        }
      });

      // Delete rows marked for deletion
      const deletePromises = deletedRows.map((row) =>
        this.heThongApi.deleteTieuChuan(row.id!),
      );

      await Promise.all([...savePromises, ...deletePromises]);

      // Remove deleted rows from grid
      this.standardRows = this.standardRows.filter((row) => !row.deleted);

      this.standardBaseline = JSON.stringify(
        this.standardRows.map((r) => this.normalizeStandardRow(r)),
      );
      this.isStandardDirty = false;
      this.notificationService.show(
        'success',
        'Lưu danh mục HTTT tiêu chuẩn thành công.',
      );
    } finally {
      this.standardSaving.set(false);
    }
  }

  private normalizeText(value: string | null | undefined): string | null {
    const normalized = value?.trim();
    return normalized ? normalized : null;
  }

  private mapDonViParentCandidatesToOptions(
    items: DonViParentCandidateDto[],
  ): Array<SelectOption<string>> {
    return items.map((item) => ({
      label: item.displayName || item.tenDonVi,
      value: item.tenDonVi,
    }));
  }

  private getDefaultStandardCatalogValues(): CodeValueDto[] {
    return [
      {
        id: 0,
        codeId: 0,
        value: 'QLVB_DIEU_HANH',
        name: 'Hệ thống quản lý văn bản và điều hành trong CAND',
        sortOrder: 1,
        isActive: true,
      },
    ];
  }

  private resolveUserDonViSubtree(tree: DonViDto[]): DonViDto[] {
    const currentDonViId = this.donViId();
    if (!Array.isArray(tree) || tree.length === 0) {
      return [];
    }

    if (currentDonViId === null || currentDonViId === undefined) {
      return tree;
    }

    const normalizedDonViId = Number(currentDonViId);
    if (!Number.isFinite(normalizedDonViId) || normalizedDonViId <= 0) {
      return tree;
    }

    const node = this.findDonViNode(tree, normalizedDonViId);
    // Fallback to full tree to avoid empty dropdown when profile donViId
    // is not present in the tree response.
    return node ? [node] : tree;
  }

  private findDonViNode(items: DonViDto[], targetId: number): DonViDto | null {
    for (const item of items) {
      if (item.id === targetId) return item;
      const found = this.findDonViNode(
        Array.isArray(item.children) ? item.children : [],
        targetId,
      );
      if (found) return found;
    }
    return null;
  }

  private flattenDonViOptions(
    items: DonViDto[],
    level = 0,
  ): Array<SelectOption<string>> {
    const result: Array<SelectOption<string>> = [];
    for (const item of items) {
      const prefix = level > 0 ? `${'\u2013\u2013'.repeat(level)} ` : '';
      result.push({ label: `${prefix}${item.tenDonVi}`, value: item.tenDonVi });
      result.push(
        ...this.flattenDonViOptions(
          Array.isArray(item.children) ? item.children : [],
          level + 1,
        ),
      );
    }
    return result;
  }
}
