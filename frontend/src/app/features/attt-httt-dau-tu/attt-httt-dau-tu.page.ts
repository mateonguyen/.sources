import { CommonModule } from '@angular/common';
import { Component, computed, signal } from '@angular/core';
import {
  FormBuilder,
  FormsModule,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { CalendarModule } from 'primeng/calendar';
import { CheckboxModule } from 'primeng/checkbox';
import { DialogModule } from 'primeng/dialog';
import { DropdownModule } from 'primeng/dropdown';
import { InputTextModule } from 'primeng/inputtext';
import { InputTextareaModule } from 'primeng/inputtextarea';
import { TableModule } from 'primeng/table';
import { TooltipModule } from 'primeng/tooltip';
import { AuthService } from '../../core/auth/auth.service';
import { NotificationService } from '../../core/ui/notification.service';
import { ConfirmDialogWrapperService } from '../../shared/ui/confirm-dialog-wrapper.service';
import { EmptyStateComponent } from '../../shared/ui/empty-state.component';
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
import { DonViApi, DonViDto } from '../don-vi/don-vi.api';
import {
  HeThongThongTinApi,
  HeThongThongTinDto,
} from '../he-thong-thong-tin/he-thong-thong-tin.api';
import {
  AtttHtttDauTuApi,
  AtttHtttDauTuDto,
  UpsertAtttHtttDauTuRequest,
} from './attt-httt-dau-tu.api';

type AtttTabKey = 'BCANET' | 'INTERNET' | 'KHAC';

interface SelectOption<T = string> {
  label: string;
  value: T;
}

interface TabDefinition {
  key: AtttTabKey;
  label: string;
  description: string;
  icon: string;
}

const CAP_DO_OPTIONS: Array<SelectOption<string>> = [
  { label: 'Cấp độ 1', value: '1' },
  { label: 'Cấp độ 2', value: '2' },
  { label: 'Cấp độ 3', value: '3' },
  { label: 'Cấp độ 4', value: '4' },
  { label: 'Cấp độ 5', value: '5' },
];

const TAB_DEFINITIONS: TabDefinition[] = [
  {
    key: 'BCANET',
    label: 'Hệ thống BCANet',
    description: 'Mạng nội bộ ngành Công an',
    icon: 'pi pi-building',
  },
  {
    key: 'INTERNET',
    label: 'Hệ thống Internet',
    description: 'Kết nối và cung cấp trên Internet',
    icon: 'pi pi-globe',
  },
  {
    key: 'KHAC',
    label: 'Hệ thống khác',
    description: 'Các phạm vi mạng còn lại',
    icon: 'pi pi-sitemap',
  },
];

@Component({
  selector: 'app-attt-httt-dau-tu-page',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    TongHopModeBannerComponent,
    SectionCardComponent,
    EmptyStateComponent,
    LoadingOverlayComponent,
    DialogModule,
    DropdownModule,
    CalendarModule,
    CheckboxModule,
    InputTextModule,
    InputTextareaModule,
    ButtonModule,
    TableModule,
    TooltipModule,
  ],
  templateUrl: './attt-httt-dau-tu.page.html',
  styleUrl: './attt-httt-dau-tu.page.scss',
})
export class AtttHtttDauTuPage {
  readonly selectPanelStyleClass = APP_SELECT_PANEL_STYLE_CLASS;
  readonly tableStyleClass = APP_TABLE_STYLE_CLASS;
  readonly tableHeaderCellClass = APP_TABLE_HEADER_CELL_CLASS;
  readonly tableRowClass = APP_TABLE_ROW_CLASS;
  readonly tableBodyCellClass = APP_TABLE_BODY_CELL_CLASS;

  readonly tabs = TAB_DEFINITIONS;
  readonly capDoOptions = CAP_DO_OPTIONS;

  readonly form = this.fb.group({
    htttId: [null as number | null, Validators.required],
    loaiHaTang: ['BCANET' as AtttTabKey, Validators.required],
    chuQuan: [null as string | null, Validators.maxLength(200)],
    donViVanHanh: [null as string | null, Validators.maxLength(200)],
    capDoDeXuat: [null as string | null],
    ngayPheDuyetHsdxcd: [null as Date | null],
    quyetDinhPheDuyet: [null as string | null, Validators.maxLength(200)],
    daLongGhepThuyetMinh: [false],
    ghiChu: [null as string | null, Validators.maxLength(2000)],
  });

  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly dialogVisible = signal(false);
  readonly items = signal<AtttHtttDauTuDto[]>([]);
  readonly htttCatalog = signal<HeThongThongTinDto[]>([]);
  readonly unitOptions = signal<SelectOption[]>([]);
  readonly selectedId = signal<number | null>(null);
  readonly selectedHtttId = signal<number | null>(null);
  readonly activeTab = signal<AtttTabKey>('BCANET');
  readonly filterTenHttt = signal('');

  readonly donViId = computed(() => this.authService.profile()?.donViId ?? 0);
  readonly totalCount = computed(() => this.items().length);
  readonly approvedCount = computed(
    () =>
      this.items().filter(
        (item) => item.ngayPheDuyetHsdxcd || item.quyetDinhPheDuyet,
      ).length,
  );
  readonly integratedCount = computed(
    () => this.items().filter((item) => item.daLongGhepThuyetMinh).length,
  );
  readonly integratedRate = computed(() =>
    this.totalCount()
      ? Math.round((this.integratedCount() / this.totalCount()) * 100)
      : 0,
  );

  readonly activeItems = computed(() => {
    const keyword = this.normalizeText(this.filterTenHttt());
    const source = this.itemsForTab(this.activeTab());
    if (!keyword) {
      return source;
    }

    return source.filter((item) => {
      const searchable = [
        this.getTenHttt(item.htttId),
        item.chuQuan,
        item.donViVanHanh,
        item.quyetDinhPheDuyet,
        item.ghiChu,
      ]
        .filter(Boolean)
        .join(' ');
      return this.normalizeText(searchable).includes(keyword);
    });
  });

  readonly activeTabDefinition = computed(
    () => this.tabs.find((tab) => tab.key === this.activeTab())!,
  );

  readonly htttOptions = computed<SelectOption<number>[]>(() => {
    const selectedId = this.selectedId();
    const selectedHtttId = this.selectedHtttId();
    const usedHtttIds = new Set(
      this.items()
        .filter((item) => item.id !== selectedId)
        .map((item) => item.htttId),
    );

    return this.htttCatalog()
      .filter((item) => item.id === selectedHtttId || !usedHtttIds.has(item.id))
      .sort((a, b) => a.tenPhanMem.localeCompare(b.tenPhanMem, 'vi'))
      .map((item) => ({ label: item.tenPhanMem, value: item.id }));
  });

  constructor(
    private readonly fb: FormBuilder,
    private readonly authService: AuthService,
    private readonly api: AtttHtttDauTuApi,
    private readonly heThongApi: HeThongThongTinApi,
    private readonly donViApi: DonViApi,
    private readonly notificationService: NotificationService,
    private readonly confirmDialog: ConfirmDialogWrapperService,
  ) {
    void this.initialize();
  }

  async initialize(): Promise<void> {
    this.loading.set(true);
    try {
      const donViId = this.donViId();
      const [httt, donVi, data] = await Promise.all([
        this.heThongApi.getAll(),
        donViId
          ? this.donViApi.getById(donViId).catch(() => null)
          : Promise.resolve(null),
        this.api.getAll({ donViId: donViId || undefined }),
      ]);

      this.htttCatalog.set(httt);
      this.items.set(data);

      if (donVi) {
        this.unitOptions.set(this.buildUnitOptions(donVi));
      }

      const firstPopulatedTab = this.tabs.find(
        (tab) => this.itemsForTab(tab.key).length > 0,
      );
      if (firstPopulatedTab) {
        this.activeTab.set(firstPopulatedTab.key);
      }
    } finally {
      this.loading.set(false);
    }
  }

  async load(force = false): Promise<void> {
    if (this.loading() && !force) {
      return;
    }

    this.loading.set(true);
    try {
      const [data, catalog] = await Promise.all([
        this.api.getAll({ donViId: this.donViId() || undefined }),
        this.heThongApi.getAll(),
      ]);
      this.items.set(data);
      this.htttCatalog.set(catalog);
    } finally {
      this.loading.set(false);
    }
  }

  setActiveTab(tab: AtttTabKey): void {
    this.activeTab.set(tab);
    this.filterTenHttt.set('');
  }

  countForTab(tab: AtttTabKey): number {
    return this.itemsForTab(tab).length;
  }

  openCreate(): void {
    this.selectedId.set(null);
    this.selectedHtttId.set(null);
    this.form.reset({
      loaiHaTang: this.activeTab(),
      daLongGhepThuyetMinh: false,
    });
    this.form.markAsPristine();
    this.form.markAsUntouched();
    this.dialogVisible.set(true);
  }

  async select(item: AtttHtttDauTuDto): Promise<void> {
    const detail = await this.api.getById(item.id);
    this.activeTab.set(this.resolveItemTab(detail));
    this.selectedId.set(detail.id);
    this.selectedHtttId.set(detail.htttId);
    this.form.reset({
      htttId: detail.htttId,
      loaiHaTang: this.resolveItemTab(detail),
      chuQuan: detail.chuQuan,
      donViVanHanh: detail.donViVanHanh,
      capDoDeXuat: detail.capDoDeXuat,
      ngayPheDuyetHsdxcd: this.parseDate(detail.ngayPheDuyetHsdxcd),
      quyetDinhPheDuyet: detail.quyetDinhPheDuyet,
      daLongGhepThuyetMinh: detail.daLongGhepThuyetMinh,
      ghiChu: detail.ghiChu,
    });
    this.form.markAsPristine();
    this.form.markAsUntouched();
    this.dialogVisible.set(true);
  }

  closeDialog(): void {
    if (!this.saving()) {
      this.dialogVisible.set(false);
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
        'Tài khoản chưa được gắn với đơn vị nhập liệu.',
      );
      return;
    }

    this.saving.set(true);
    try {
      const raw = this.form.getRawValue();
      const payload: UpsertAtttHtttDauTuRequest = {
        donViId,
        htttId: raw.htttId!,
        loaiHaTang: raw.loaiHaTang!,
        chuQuan: raw.chuQuan?.trim() || null,
        donViVanHanh: raw.donViVanHanh?.trim() || null,
        capDoDeXuat: raw.capDoDeXuat?.trim() || null,
        ngayPheDuyetHsdxcd: this.toApiDate(raw.ngayPheDuyetHsdxcd),
        quyetDinhPheDuyet: raw.quyetDinhPheDuyet?.trim() || null,
        daLongGhepThuyetMinh: raw.daLongGhepThuyetMinh ?? false,
        ghiChu: raw.ghiChu?.trim() || null,
      };

      const selectedId = this.selectedId();
      const saved =
        selectedId === null
          ? await this.api.create(payload)
          : await this.api.update(selectedId, payload);

      this.dialogVisible.set(false);
      this.notificationService.show(
        'success',
        selectedId === null
          ? 'Đã thêm HTTT vào danh sách đầu tư.'
          : 'Đã cập nhật thông tin ATTT HTTT đầu tư.',
      );
      await this.load(true);
      this.activeTab.set(this.resolveItemTab(saved));
    } finally {
      this.saving.set(false);
    }
  }

  async remove(item: AtttHtttDauTuDto): Promise<void> {
    const confirmed = await this.confirmDialog.confirmDelete({
      message: `Xác nhận xóa khai báo ATTT của “${this.getTenHttt(item.htttId)}”? Dữ liệu các báo cáo đã nộp không bị ảnh hưởng.`,
      acceptLabel: 'Xóa khai báo',
      rejectLabel: 'Giữ lại',
    });
    if (!confirmed) {
      return;
    }

    await this.api.delete(item.id);
    this.notificationService.show('success', 'Đã xóa khai báo khỏi dữ liệu live.');
    await this.load(true);
  }

  handleEmptyAction(): void {
    if (!this.filterTenHttt()) {
      this.openCreate();
      return;
    }
    this.filterTenHttt.set('');
    void this.load(true);
  }

  getTenHttt(htttId: number): string {
    return (
      this.htttCatalog().find((item) => item.id === htttId)?.tenPhanMem ??
      `HTTT #${htttId}`
    );
  }

  getPhamViHttt(htttId: number): string {
    const item = this.htttCatalog().find((entry) => entry.id === htttId);
    return (
      item?.phamViHoatDongKyThuat || item?.phamViHoatDong || 'Chưa xác định'
    );
  }

  formatDisplayDate(value: string | null): string {
    const parsed = this.parseDate(value);
    if (!parsed) {
      return value || '—';
    }
    return new Intl.DateTimeFormat('vi-VN').format(parsed);
  }

  resolveCapDo(value: string | null): string {
    if (!value) {
      return 'Chưa đề xuất';
    }
    return CAP_DO_OPTIONS.find((item) => item.value === value)?.label ?? value;
  }

  private itemsForTab(tab: AtttTabKey): AtttHtttDauTuDto[] {
    return this.items().filter((item) => this.resolveItemTab(item) === tab);
  }

  private buildUnitOptions(donVi: DonViDto): SelectOption[] {
    return [donVi, ...donVi.children]
      .map((item) => ({ label: item.tenDonVi, value: item.tenDonVi }))
      .filter(
        (option, index, options) =>
          options.findIndex((item) => item.value === option.value) === index,
      );
  }

  private parseDate(value: string | null | undefined): Date | null {
    if (!value) {
      return null;
    }
    const parsed = new Date(value);
    return Number.isNaN(parsed.getTime()) ? null : parsed;
  }

  private toApiDate(value: Date | null | undefined): string | null {
    if (!value) {
      return null;
    }
    const year = value.getFullYear();
    const month = String(value.getMonth() + 1).padStart(2, '0');
    const day = String(value.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }

  private resolveItemTab(item: AtttHtttDauTuDto): AtttTabKey {
    const value = item.loaiHaTang?.trim().toUpperCase();
    return value === 'BCANET' || value === 'INTERNET' ? value : 'KHAC';
  }

  private normalizeText(value: string): string {
    return value
      .trim()
      .toLowerCase()
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .replace(/đ/g, 'd');
  }
}
