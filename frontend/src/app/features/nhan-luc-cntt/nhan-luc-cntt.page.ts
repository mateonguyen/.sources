import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { PaginatorState } from 'primeng/paginator';
import { AuthService } from '../../core/auth/auth.service';
import { NotificationService } from '../../core/ui/notification.service';
import { ConfirmDialogWrapperService } from '../../shared/ui/confirm-dialog-wrapper.service';
import { SectionCardComponent } from '../../shared/ui/section-card.component';
import { CodesApi } from '../codes/codes.api';
import { DonViApi, DonViDto } from '../don-vi/don-vi.api';
import { NhanLucCnttApi } from './nhan-luc-cntt.api';
import { NhanLucCnttFilterComponent } from './nhan-luc-cntt-filter.component';
import {
  NhanLucCnttDialogMode,
  NhanLucCnttUpsertDialogComponent,
} from './nhan-luc-cntt-upsert-dialog.component';
import {
  NHAN_LUC_CNTT_PAGE_SIZE_OPTIONS,
  NhanLucCnttFilterValue,
  NhanLucCnttItem,
  NhanLucCnttSearchParams,
  NhanLucCnttTableRow,
  NhanLucCnttUpsertInitialData,
  NhanLucCnttUpsertSubmitEvent,
  SelectOption,
} from './nhan-luc-cntt.models';
import { NhanLucCnttTableComponent } from './nhan-luc-cntt-table.component';

import { TongHopModeBannerComponent } from '../../shared/ui/tong-hop-mode-banner.component';
interface FlatDonViOption {
  id: number;
  parentId?: number;
  label: string;
}

@Component({
  selector: 'app-nhan-luc-cntt-page',
  standalone: true,
  imports: [
    TongHopModeBannerComponent,
    CommonModule,
    ReactiveFormsModule,
    SectionCardComponent,
    NhanLucCnttFilterComponent,
    NhanLucCnttTableComponent,
    NhanLucCnttUpsertDialogComponent,
  ],
  templateUrl: './nhan-luc-cntt.page.html',
  styleUrl: './nhan-luc-cntt.page.scss',
})
export class NhanLucCnttPage {
  private static readonly CROSS_DON_VI_PERMISSION = 'ky_bao_cao:approve';

  readonly pageSizeOptions = [...NHAN_LUC_CNTT_PAGE_SIZE_OPTIONS];

  readonly filterForm = this.formBuilder.group({
    tuKhoa: [''],
    namBaoCao: [new Date().getFullYear() as number | null],
    donViCongTacId: [null as number | null],
    gioiTinh: [null as string | null],
    capBac: [null as string | null],
    loaiNhanLuc: [null as string | null],
    trinhDoCntt: [null as string | null],
  });

  loading = false;
  dialogSubmitting = false;
  advancedVisible = false;
  dialogVisible = false;
  dialogMode: NhanLucCnttDialogMode = 'create';
  dialogInitialData: NhanLucCnttUpsertInitialData | null = null;

  first = 0;
  rows = 10;
  totalRecords = 0;
  overallRecords = 0;

  items: NhanLucCnttItem[] = [];
  tableRows: NhanLucCnttTableRow[] = [];

  namOptions: SelectOption<number | null>[] = [];
  donViFilterOptions: SelectOption<number | null>[] = [];
  donViDialogOptions: SelectOption<number>[] = [];
  donViCongTacDialogOptions: SelectOption<number | null>[] = [];
  gioiTinhFilterOptions: SelectOption<string | null>[] = [];
  gioiTinhDialogOptions: SelectOption<string | null>[] = [];
  capBacFilterOptions: SelectOption<string | null>[] = [];
  capBacDialogOptions: SelectOption<string | null>[] = [];
  loaiNhanLucFilterOptions: SelectOption<string | null>[] = [];
  loaiNhanLucDialogOptions: SelectOption<string | null>[] = [];
  trinhDoCnttFilterOptions: SelectOption<string | null>[] = [];
  trinhDoCnttDialogOptions: SelectOption<string | null>[] = [];
  trinhDoLlctDialogOptions: SelectOption<string | null>[] = [];

  private allDonVis: FlatDonViOption[] = [];
  private donViMap = new Map<number, string>();
  private codeMaps = new Map<string, Map<string, string>>();
  defaultDonViId: number | null = null;

  constructor(
    private readonly formBuilder: FormBuilder,
    private readonly nhanLucApi: NhanLucCnttApi,
    private readonly codesApi: CodesApi,
    private readonly donViApi: DonViApi,
    private readonly authService: AuthService,
    private readonly notificationService: NotificationService,
    private readonly confirmDialog: ConfirmDialogWrapperService,
  ) {
    void this.initialize();
  }

  get canSelectReportDonVi(): boolean {
    return this.authService.hasPermission(
      NhanLucCnttPage.CROSS_DON_VI_PERMISSION,
    );
  }

  get reportDonViLabel(): string {
    return this.resolveDonViLabel(this.defaultDonViId) || 'Chưa xác định';
  }

  async initialize(): Promise<void> {
    this.loading = true;
    try {
      const [
        gioiTinh,
        capBac,
        loaiNhanLuc,
        trinhDoCntt,
        trinhDoLlct,
        donViTree,
      ] = await Promise.all([
        this.codesApi.getByCode('GIOI_TINH'),
        this.codesApi.getByCode('CAP_BAC_CONG_AN'),
        this.codesApi.getByCode('LOAI_NHAN_LUC'),
        this.codesApi.getByCode('TRINH_DO_CNTT'),
        this.codesApi.getByCode('TRINH_DO_LLCT'),
        this.donViApi.getTree(),
      ]);

      this.allDonVis = this.flattenDonViTree(donViTree);
      this.donViMap = new Map(
        this.allDonVis.map((item) => [item.id, item.label]),
      );
      const profileDonViId = this.authService.profile()?.donViId ?? null;
      this.defaultDonViId =
        this.resolveInitialReportDonViId(profileDonViId) ??
        this.allDonVis[0]?.id ??
        null;
      this.namOptions = this.buildYearOptions();

      this.gioiTinhFilterOptions = this.toCodeOptions(
        gioiTinh.values,
        'Tất cả giới tính',
      );
      this.gioiTinhDialogOptions = this.toCodeOptions(
        gioiTinh.values,
        'Không chọn',
      );
      this.capBacFilterOptions = this.toCodeOptions(
        capBac.values,
        'Tất cả cấp bậc',
      );
      this.capBacDialogOptions = this.toCodeOptions(
        capBac.values,
        'Không chọn',
      );
      this.loaiNhanLucFilterOptions = this.toCodeOptions(
        loaiNhanLuc.values,
        'Tất cả loại nhân lực',
      );
      this.loaiNhanLucDialogOptions = this.toCodeOptions(
        loaiNhanLuc.values,
        'Không chọn',
      );
      this.trinhDoCnttFilterOptions = this.toCodeOptions(
        trinhDoCntt.values,
        'Tất cả trình độ CNTT',
      );
      this.trinhDoCnttDialogOptions = this.toCodeOptions(
        trinhDoCntt.values,
        'Không chọn',
      );
      this.trinhDoLlctDialogOptions = this.toCodeOptions(
        trinhDoLlct.values,
        'Không chọn',
      );

      this.codeMaps.set('gioiTinh', this.toCodeMap(gioiTinh.values));
      this.codeMaps.set('capBac', this.toCodeMap(capBac.values));
      this.codeMaps.set('loaiNhanLuc', this.toCodeMap(loaiNhanLuc.values));
      this.codeMaps.set('trinhDoCntt', this.toCodeMap(trinhDoCntt.values));
      this.codeMaps.set('trinhDoLlct', this.toCodeMap(trinhDoLlct.values));

      this.donViFilterOptions = [
        { label: 'Tất cả đơn vị', value: null },
        ...this.allDonVis.map((item) => ({
          label: item.label,
          value: item.id,
        })),
      ];
      this.donViDialogOptions = this.buildDonViDialogOptions();

      this.rebuildDonViCongTacDialogOptions();
      await this.loadPage(true);
    } finally {
      this.loading = false;
    }
  }

  get summary(): string {
    if (this.totalRecords === 0) {
      return 'Hiển thị 0-0 trong tổng số 0 nhân lực';
    }

    const from = this.first + 1;
    const to = Math.min(this.first + this.items.length, this.totalRecords);
    return `Hiển thị ${from}-${to} trong tổng số ${this.totalRecords} nhân lực`;
  }

  get advancedFilterCount(): number {
    const value = this.filterForm.getRawValue();
    return [
      value.gioiTinh,
      value.capBac,
      value.loaiNhanLuc,
      value.trinhDoCntt,
    ].filter((item) => item !== null && item !== undefined && item !== '')
      .length;
  }

  applyFilters(): void {
    void this.loadPage(true);
  }

  clearFilters(): void {
    this.filterForm.reset({
      tuKhoa: '',
      namBaoCao: new Date().getFullYear(),
      donViCongTacId: null,
      gioiTinh: null,
      capBac: null,
      loaiNhanLuc: null,
      trinhDoCntt: null,
    });
    void this.loadPage(true);
  }

  toggleAdvancedFilters(): void {
    this.advancedVisible = !this.advancedVisible;
  }

  openCreateDialog(): void {
    this.dialogMode = 'create';
    this.dialogInitialData = {
      donViId: this.defaultDonViId,
      donViCongTacId: null,
      hoTen: '',
      ngaySinh: '',
      gioiTinh: null,
      capBac: null,
      chucVu: '',
      dienThoai: '',
      loaiNhanLuc: null,
      trinhDoCntt: null,
      trinhDoLlct: null,
      ghiChu: '',
    };
    this.rebuildDonViCongTacDialogOptions(this.defaultDonViId);
    this.dialogVisible = true;
  }

  async openEditDialog(row: NhanLucCnttTableRow): Promise<void> {
    const detail = await this.nhanLucApi.getById(row.id);
    this.openEditDialogWithDetail(detail);
  }

  async onDialogSave(event: NhanLucCnttUpsertSubmitEvent): Promise<void> {
    if (this.dialogSubmitting) {
      return;
    }

    this.dialogSubmitting = true;
    try {
      const payload = {
        ...event.payload,
        donViId: this.canSelectReportDonVi
          ? event.payload.donViId
          : (this.defaultDonViId ?? event.payload.donViId),
      };

      if (this.dialogMode === 'create') {
        await this.nhanLucApi.create(payload);
        this.notificationService.show(
          'success',
          'Thêm nhân lực CNTT thành công.',
        );
      } else if (event.id) {
        await this.nhanLucApi.update(event.id, payload);
        this.notificationService.show(
          'success',
          'Cập nhật nhân lực CNTT thành công.',
        );
      }

      await this.loadPage(true);

      if (event.keepDialogOpen) {
        this.openCreateDialog();
      } else {
        this.dialogVisible = false;
      }
    } finally {
      this.dialogSubmitting = false;
    }
  }

  async onDelete(row: NhanLucCnttTableRow): Promise<void> {
    const confirmed = await this.confirmDialog.confirmDelete({
      message: `Xác nhận xóa hồ sơ của “${row.hoTen}”?`,
      acceptLabel: 'Xóa hồ sơ',
      rejectLabel: 'Hủy',
    });
    if (!confirmed) {
      return;
    }

    await this.nhanLucApi.delete(row.id);
    this.notificationService.show(
      'success',
      'Xóa hồ sơ nhân lực CNTT thành công.',
    );
    await this.loadPage(this.items.length === 1 && this.first > 0);
  }

  onPageChange(event: PaginatorState): void {
    const nextRows = event.rows ?? this.rows;
    const rowsChanged = nextRows !== this.rows;

    this.rows = nextRows;
    this.first = rowsChanged ? 0 : (event.first ?? 0);
    void this.loadPage(false);
  }

  onRowsChange(rows: number): void {
    this.rows = rows;
    this.first = 0;
    void this.loadPage(true);
  }

  onDialogVisibleChange(visible: boolean): void {
    this.dialogVisible = visible;
  }

  onDialogDonViChange(donViId: number | null): void {
    if (!this.canSelectReportDonVi) {
      return;
    }

    this.rebuildDonViCongTacDialogOptions(donViId);
  }

  private async loadPage(resetPaging = false): Promise<void> {
    if (resetPaging) {
      this.first = 0;
    }

    this.loading = true;
    try {
      const result = await this.nhanLucApi.search(this.buildSearchParams());
      this.items = result.items;
      this.tableRows = result.items.map((item) => this.toTableRow(item));
      this.totalRecords = result.totalItems;
      this.overallRecords = result.totalItems;
    } finally {
      this.loading = false;
    }
  }

  private buildSearchParams(): NhanLucCnttSearchParams {
    const filters = this.filterForm.getRawValue() as NhanLucCnttFilterValue;
    return {
      page: Math.floor(this.first / this.rows) + 1,
      pageSize: this.rows,
      tuKhoa: filters.tuKhoa?.trim() || undefined,
      namBaoCao: filters.namBaoCao,
      donViCongTacId: filters.donViCongTacId,
      gioiTinh: filters.gioiTinh,
      capBac: filters.capBac,
      loaiNhanLuc: filters.loaiNhanLuc,
      trinhDoCntt: filters.trinhDoCntt,
    };
  }

  private openEditDialogWithDetail(detail: NhanLucCnttItem): void {
    this.dialogMode = 'edit';
    this.dialogInitialData = {
      id: detail.id,
      donViId: detail.donViId,
      donViCongTacId: detail.donViCongTacId ?? null,
      hoTen: detail.hoTen,
      ngaySinh: detail.ngaySinh ?? '',
      gioiTinh: detail.gioiTinh ?? null,
      capBac: detail.capBac ?? null,
      chucVu: detail.chucVu ?? '',
      dienThoai: detail.dienThoai ?? '',
      loaiNhanLuc: detail.loaiNhanLuc ?? null,
      trinhDoCntt: detail.trinhDoCntt ?? null,
      trinhDoLlct: detail.trinhDoLlct ?? null,
      ghiChu: detail.ghiChu ?? '',
    };
    this.rebuildDonViCongTacDialogOptions(detail.donViId);
    this.dialogVisible = true;
  }

  private toTableRow(item: NhanLucCnttItem): NhanLucCnttTableRow {
    const capBacText = this.resolveCodeLabel('capBac', item.capBac, '');
    const hoTenDayDu = [capBacText, item.hoTen].filter(Boolean).join(' ');

    return {
      id: item.id,
      hoTen: item.hoTen,
      hoTenDayDu,
      capBacText: capBacText || '—',
      chucVuText: item.chucVu?.trim() || 'Chưa cập nhật',
      donViCongTacText:
        item.donViCongTacTen ||
        this.resolveDonViLabel(item.donViCongTacId) ||
        item.donViTen ||
        this.resolveDonViLabel(item.donViId) ||
        'Chưa cập nhật',
      loaiNhanLucText: this.resolveCodeLabel(
        'loaiNhanLuc',
        item.loaiNhanLuc,
        'Chưa phân loại',
      ),
      trinhDoCnttText: this.resolveCodeLabel(
        'trinhDoCntt',
        item.trinhDoCntt,
        'Chưa cập nhật',
      ),
      dienThoaiText: item.dienThoai?.trim() || 'Chưa cập nhật',
      gioiTinhText: this.resolveCodeLabel('gioiTinh', item.gioiTinh, 'Chưa rõ'),
      ngaySinhText: item.ngaySinh ? this.formatDate(item.ngaySinh) : '',
      raw: item,
    };
  }

  private resolveCodeLabel(
    key: string,
    value: string | null | undefined,
    fallback: string,
  ): string {
    if (!value) {
      return fallback;
    }

    return this.codeMaps.get(key)?.get(value) ?? value;
  }

  private resolveDonViLabel(id: number | null | undefined): string {
    if (!id) {
      return '';
    }

    return this.donViMap.get(id) ?? '';
  }

  private flattenDonViTree(items: ReadonlyArray<DonViDto>): FlatDonViOption[] {
    return items.flatMap((item) => [
      {
        id: item.id,
        parentId: item.parentId,
        label: `${item.maDonVi} - ${item.tenDonVi}`,
      },
      ...this.flattenDonViTree(item.children ?? []),
    ]);
  }

  private toCodeOptions(
    values: ReadonlyArray<{ value: string; name: string }>,
    emptyLabel: string,
  ): SelectOption<string | null>[] {
    return [
      { label: emptyLabel, value: null },
      ...values.map((item) => ({ label: item.name, value: item.value })),
    ];
  }

  private toCodeMap(
    values: ReadonlyArray<{ value: string; name: string }>,
  ): Map<string, string> {
    return new Map(values.map((item) => [item.value, item.name]));
  }

  private buildYearOptions(): SelectOption<number | null>[] {
    const currentYear = new Date().getFullYear();
    return Array.from({ length: 5 }, (_, index) => currentYear - index).map(
      (year) => ({ label: `${year}`, value: year }),
    );
  }

  private buildDonViDialogOptions(): SelectOption<number>[] {
    if (this.canSelectReportDonVi) {
      return this.allDonVis.map((item) => ({
        label: item.label,
        value: item.id,
      }));
    }

    const defaultOption = this.allDonVis.find(
      (item) => item.id === this.defaultDonViId,
    );

    return defaultOption
      ? [{ label: defaultOption.label, value: defaultOption.id }]
      : [];
  }

  private resolveInitialReportDonViId(
    profileDonViId: number | null,
  ): number | null {
    if (!profileDonViId) {
      return null;
    }

    const matchedDonVi = this.allDonVis.find(
      (item) => item.id === profileDonViId,
    );
    return matchedDonVi?.id ?? null;
  }

  private rebuildDonViCongTacDialogOptions(
    donViId = this.defaultDonViId,
  ): void {
    const selectedParentId = donViId ?? this.defaultDonViId ?? null;
    this.donViCongTacDialogOptions = [
      { label: 'Không chọn', value: null },
      ...this.allDonVis
        .filter(
          (item) =>
            !selectedParentId ||
            item.id === selectedParentId ||
            item.parentId === selectedParentId,
        )
        .map((item) => ({ label: item.label, value: item.id })),
    ];
  }

  private formatDate(value: string): string {
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
      return value;
    }

    return date.toLocaleDateString('vi-VN');
  }
}
