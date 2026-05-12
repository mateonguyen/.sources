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
import { TableModule } from 'primeng/table';
import { TooltipModule } from 'primeng/tooltip';
import { AuthService } from '../../core/auth/auth.service';
import { NotificationService } from '../../core/ui/notification.service';
import { ConfirmDialogWrapperService } from '../../shared/ui/confirm-dialog-wrapper.service';
import { EmptyStateComponent } from '../../shared/ui/empty-state.component';
import { FilterBarComponent } from '../../shared/ui/filter-bar.component';
import { FormActionBarComponent } from '../../shared/ui/form-action-bar.component';
import { LoadingOverlayComponent } from '../../shared/ui/loading-overlay.component';
import { SectionCardComponent } from '../../shared/ui/section-card.component';
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
import { KyBaoCaoApi, KyBaoCaoDto } from '../ky-bao-cao/ky-bao-cao.api';
import { BusinessModulesApi } from '../business-modules/business-modules.api';
import {
  AtttHtttDauTuApi,
  AtttHtttDauTuDto,
  UpsertAtttHtttDauTuRequest,
} from './attt-httt-dau-tu.api';

type ModuleViewMode = 'live' | 'history';
type AtttTabKey = 'BCANET' | 'INTERNET' | 'KHAC';

interface SelectOption<T = string> {
  label: string;
  value: T;
}

const CAP_DO_OPTIONS: Array<SelectOption<string>> = [
  { label: 'Cấp 1', value: '1' },
  { label: 'Cấp 2', value: '2' },
  { label: 'Cấp 3', value: '3' },
  { label: 'Cấp 4', value: '4' },
  { label: 'Cấp 5', value: '5' },
];

@Component({
  selector: 'app-attt-httt-dau-tu-page',
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
    DialogModule,
    DropdownModule,
    CalendarModule,
    CheckboxModule,
    InputTextModule,
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

  readonly filter = this.fb.group({
    viewMode: ['live' as ModuleViewMode],
    kyCode: [''],
  });

  readonly viewModeOptions: Array<{ label: string; value: ModuleViewMode }> = [
    { label: 'Live', value: 'live' },
    { label: 'Lịch sử', value: 'history' },
  ];

  readonly form = this.fb.group({
    htttId: [null as number | null, Validators.required],
    chuQuan: [null as string | null],
    donViVanHanh: [null as string | null],
    capDoDeXuat: [null as string | null],
    ngayPheDuyetHsdxcd: [null as Date | null],
    quyetDinhPheDuyet: [null as string | null],
    daLongGhepThuyetMinh: [false],
    ghiChu: [null as string | null],
  });

  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly dialogVisible = signal(false);
  readonly items = signal<AtttHtttDauTuDto[]>([]);
  readonly kyBaoCaos = signal<KyBaoCaoDto[]>([]);
  readonly htttCatalog = signal<HeThongThongTinDto[]>([]);
  readonly unitOptions = signal<SelectOption[]>([]);
  readonly selectedId = signal<number | null>(null);
  readonly activeTab = signal<AtttTabKey>('BCANET');
  readonly filterTenHttt = signal('');

  readonly capDoOptions = CAP_DO_OPTIONS;

  readonly donViId = computed(() => this.authService.profile()?.donViId ?? 0);

  readonly isHistoryMode = computed(
    () => this.filter.getRawValue().viewMode === 'history',
  );

  readonly canEdit = computed(() => !this.isHistoryMode());

  readonly htttOptions = computed<SelectOption<number>[]>(() =>
    this.htttCatalog().map((item) => ({
      label: item.tenPhanMem,
      value: item.id,
    })),
  );

  readonly kyOptions = computed(() =>
    this.kyBaoCaos().map((item) => ({
      label: item.kyCode,
      value: item.kyCode,
    })),
  );

  readonly bcanetItems = computed(() =>
    this.items().filter((item) => this.resolveItemTab(item) === 'BCANET'),
  );

  readonly internetItems = computed(() =>
    this.items().filter((item) => this.resolveItemTab(item) === 'INTERNET'),
  );

  readonly khacItems = computed(() =>
    this.items().filter((item) => this.resolveItemTab(item) === 'KHAC'),
  );

  readonly bcanetCount = computed(() => this.bcanetItems().length);
  readonly internetCount = computed(() => this.internetItems().length);
  readonly khacCount = computed(() => this.khacItems().length);

  readonly activeItems = computed(() => {
    const keyword = this.filterTenHttt().trim().toLowerCase();
    const tab = this.activeTab();
    const source =
      tab === 'BCANET'
        ? this.bcanetItems()
        : tab === 'INTERNET'
          ? this.internetItems()
          : this.khacItems();

    if (!keyword) {
      return source;
    }

    return source.filter((item) =>
      this.getTenHttt(item.htttId).toLowerCase().includes(keyword),
    );
  });

  constructor(
    private readonly fb: FormBuilder,
    private readonly authService: AuthService,
    private readonly api: AtttHtttDauTuApi,
    private readonly kyBaoCaoApi: KyBaoCaoApi,
    private readonly heThongApi: HeThongThongTinApi,
    private readonly donViApi: DonViApi,
    private readonly businessApi: BusinessModulesApi,
    private readonly notificationService: NotificationService,
    private readonly confirmDialog: ConfirmDialogWrapperService,
  ) {
    void this.initialize();
  }

  async initialize(): Promise<void> {
    this.loading.set(true);
    try {
      const donViId = this.donViId();
      const [httt, currentKy, allKy, donVi] = await Promise.all([
        this.heThongApi.getAll(),
        this.kyBaoCaoApi.getCurrent().catch(() => null),
        this.kyBaoCaoApi.getAll().catch(() => []),
        donViId
          ? this.donViApi.getById(donViId).catch(() => null)
          : Promise.resolve(null),
      ]);

      this.htttCatalog.set(httt);
      this.kyBaoCaos.set(allKy);

      const initialKyCode = currentKy?.kyCode ?? allKy[0]?.kyCode ?? '';
      this.filter.patchValue({ kyCode: initialKyCode }, { emitEvent: false });

      if (donVi) {
        this.unitOptions.set(this.buildUnitOptions(donVi));
      }

      await this.load(true);
    } finally {
      this.loading.set(false);
    }
  }

  async onFilterChanged(): Promise<void> {
    await this.load(true);
  }

  setActiveTab(tab: AtttTabKey): void {
    this.activeTab.set(tab);
    this.filterTenHttt.set('');
  }

  async load(force = false): Promise<void> {
    if (this.loading() && !force) {
      return;
    }

    this.loading.set(true);
    try {
      const kyCode = String(this.filter.getRawValue().kyCode ?? '').trim();
      const selectedKyCode = this.isHistoryMode() ? kyCode : undefined;
      const data = await this.api.getAll({
        donViId: this.donViId() || undefined,
        kyCode: selectedKyCode,
      });
      this.items.set(data);
    } finally {
      this.loading.set(false);
    }
  }

  openCreate(): void {
    if (!this.canEdit()) {
      return;
    }

    this.selectedId.set(null);
    this.form.reset({
      daLongGhepThuyetMinh: false,
    });
    this.form.markAsPristine();
    this.form.markAsUntouched();
    this.dialogVisible.set(true);
  }

  async select(item: AtttHtttDauTuDto): Promise<void> {
    const detail = await this.api.getById(item.id);
    this.selectedId.set(detail.id);
    this.form.patchValue({
      htttId: detail.htttId,
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

  resetForm(): void {
    this.selectedId.set(null);
    this.form.reset({ daLongGhepThuyetMinh: false });
    this.form.markAsPristine();
    this.form.markAsUntouched();
  }

  closeDialog(): void {
    this.dialogVisible.set(false);
  }

  async save(): Promise<void> {
    if (!this.canEdit()) {
      this.notificationService.show(
        'warning',
        'Chế độ lịch sử chỉ cho phép xem.',
      );
      return;
    }

    if (this.form.invalid || this.saving()) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    try {
      const raw = this.form.getRawValue();
      const payload: UpsertAtttHtttDauTuRequest = {
        donViId: this.donViId(),
        htttId: raw.htttId!,
        chuQuan: raw.chuQuan?.trim() || null,
        donViVanHanh: raw.donViVanHanh?.trim() || null,
        capDoDeXuat: raw.capDoDeXuat?.trim() || null,
        ngayPheDuyetHsdxcd: this.toApiDate(raw.ngayPheDuyetHsdxcd),
        quyetDinhPheDuyet: raw.quyetDinhPheDuyet?.trim() || null,
        daLongGhepThuyetMinh: raw.daLongGhepThuyetMinh ?? false,
        ghiChu: raw.ghiChu?.trim() || null,
      };

      const selectedId = this.selectedId();
      if (selectedId !== null) {
        await this.api.update(selectedId, payload);
        this.notificationService.show(
          'success',
          'Cập nhật ATTT HTTT đầu tư thành công.',
        );
      } else {
        await this.api.create(payload);
        this.notificationService.show(
          'success',
          'Thêm mới ATTT HTTT đầu tư thành công.',
        );
      }

      this.closeDialog();
      this.resetForm();
      await this.load(true);
    } finally {
      this.saving.set(false);
    }
  }

  async remove(item: AtttHtttDauTuDto): Promise<void> {
    if (!this.canEdit()) {
      return;
    }

    const confirmed = await this.confirmDialog.confirmDelete({
      message: `Xác nhận xóa bản ghi "${this.getTenHttt(item.htttId)}"?`,
      acceptLabel: 'Xóa',
      rejectLabel: 'Hủy',
    });
    if (!confirmed) {
      return;
    }

    await this.api.delete(item.id);
    if (this.selectedId() === item.id) {
      this.resetForm();
    }
    this.notificationService.show('success', 'Xóa bản ghi thành công.');
    await this.load(true);
  }

  async finalizeModule(): Promise<void> {
    if (!this.canEdit() || this.saving()) {
      return;
    }

    const kyBaoCaoCode = String(this.filter.getRawValue().kyCode ?? '').trim();
    const donViId = this.donViId();
    if (!kyBaoCaoCode || !donViId) {
      this.notificationService.show(
        'error',
        'Vui lòng chọn kỳ báo cáo hợp lệ để chốt kỳ.',
      );
      return;
    }

    const confirmed = await this.confirmDialog.confirm({
      header: 'Chốt kỳ ATTT HTTT đầu tư',
      message: `Xác nhận chốt dữ liệu kỳ ${kyBaoCaoCode} cho đơn vị hiện tại?`,
      acceptLabel: 'Chốt kỳ',
      rejectLabel: 'Hủy',
    });
    if (!confirmed) {
      return;
    }

    this.saving.set(true);
    try {
      await this.businessApi.finalizePeriod('attt-httt-dau-tu', {
        donViId,
        kyBaoCaoCode,
      });
      this.notificationService.show(
        'success',
        `Đã chốt kỳ ${kyBaoCaoCode} thành công.`,
      );
      await this.load(true);
    } finally {
      this.saving.set(false);
    }
  }

  getTenHttt(htttId: number): string {
    return (
      this.htttCatalog().find((item) => item.id === htttId)?.tenPhanMem ??
      `HTTT #${htttId}`
    );
  }

  formatDisplayDate(value: string | null): string {
    if (!value) {
      return '—';
    }

    const parsed = this.parseDate(value);
    if (!parsed) {
      return value;
    }

    const day = String(parsed.getDate()).padStart(2, '0');
    const month = String(parsed.getMonth() + 1).padStart(2, '0');
    const year = parsed.getFullYear();
    return `${day}/${month}/${year}`;
  }

  resolveCapDo(value: string | null): string {
    if (!value) {
      return '—';
    }

    const option = CAP_DO_OPTIONS.find((item) => item.value === value);
    return option?.label ?? `Cấp ${value}`;
  }

  private buildUnitOptions(donVi: DonViDto): SelectOption[] {
    const options: SelectOption[] = [
      {
        label: donVi.tenDonVi,
        value: donVi.tenDonVi,
      },
    ];

    for (const child of donVi.children) {
      options.push({
        label: child.tenDonVi,
        value: child.tenDonVi,
      });
    }

    return options;
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

    const date = new Date(value);
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }

  private resolveItemTab(item: AtttHtttDauTuDto): AtttTabKey {
    const httt = this.htttCatalog().find((entry) => entry.id === item.htttId);
    if (!httt) {
      return 'KHAC';
    }

    const text =
      `${httt.phamViHoatDongKyThuat ?? ''} ${httt.phamViHoatDong ?? ''} ${httt.tenPhanMem ?? ''}`
        .toLowerCase()
        .normalize('NFD')
        .replace(/[\u0300-\u036f]/g, '');

    if (text.includes('bcanet') || text.includes('noi bo')) {
      return 'BCANET';
    }

    if (text.includes('internet')) {
      return 'INTERNET';
    }

    return 'KHAC';
  }
}
