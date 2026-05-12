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
import { HeThongThongTinApi, HeThongThongTinDto } from '../he-thong-thong-tin/he-thong-thong-tin.api';
import {
  AtttHtttVanHanhApi,
  AtttHtttVanHanhDto,
  UpsertAtttHtttVanHanhRequest,
} from './attt-httt-van-hanh.api';

type AtttTabKey = 'BCANET' | 'INTERNET' | 'KHAC';

interface SelectOption<T = string> {
  label: string;
  value: T;
}

const CAP_DO_OPTIONS: SelectOption[] = [
  { label: 'Cấp 1', value: 'CAP_1' },
  { label: 'Cấp 2', value: 'CAP_2' },
  { label: 'Cấp 3', value: 'CAP_3' },
  { label: 'Cấp 4', value: 'CAP_4' },
  { label: 'Cấp 5', value: 'CAP_5' },
];

const TINH_TRANG_OPTIONS: SelectOption[] = [
  { label: 'Chưa phê duyệt', value: 'CHUA_PHE_DUYET' },
  { label: 'Đang xử lý', value: 'DANG_XU_LY' },
  { label: 'Đã phê duyệt', value: 'DA_PHE_DUYET' },
];

const LOAI_HA_TANG_OPTIONS: SelectOption[] = [
  { label: 'BCANet', value: 'BCANET' },
  { label: 'Internet', value: 'INTERNET' },
  { label: 'Khác', value: 'KHAC' },
];

@Component({
  selector: 'app-attt-httt-van-hanh-page',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    SectionCardComponent,
    FilterBarComponent,
    EmptyStateComponent,
    LoadingOverlayComponent,
    DialogModule,
    DropdownModule,
    CheckboxModule,
    CalendarModule,
    InputTextModule,
    ButtonModule,
    TableModule,
    TooltipModule,
  ],
  templateUrl: './attt-httt-van-hanh.page.html',
  styleUrl: './attt-httt-van-hanh.page.scss',
})
export class AtttHtttVanHanhPage {
  readonly selectPanelStyleClass = APP_SELECT_PANEL_STYLE_CLASS;
  readonly tableStyleClass = APP_TABLE_STYLE_CLASS;
  readonly tableHeaderCellClass = APP_TABLE_HEADER_CELL_CLASS;
  readonly tableRowClass = APP_TABLE_ROW_CLASS;
  readonly tableBodyCellClass = APP_TABLE_BODY_CELL_CLASS;

  readonly donViId = computed(() => this.authService.profile()?.donViId ?? 0);

  readonly items = signal<AtttHtttVanHanhDto[]>([]);
  readonly htttCatalog = signal<HeThongThongTinDto[]>([]);
  readonly unitOptions = signal<SelectOption[]>([]);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly dialogVisible = signal(false);
  readonly selectedId = signal<number | null>(null);
  readonly activeTab = signal<AtttTabKey>('BCANET');
  readonly filterTenHttt = signal('');

  readonly capDoOptions = CAP_DO_OPTIONS;
  readonly tinhTrangOptions = TINH_TRANG_OPTIONS;
  readonly loaiHaTangOptions = LOAI_HA_TANG_OPTIONS;

  readonly htttOptions = computed<SelectOption<number>[]>(() =>
    this.htttCatalog().map((h) => ({ label: h.tenPhanMem, value: h.id })),
  );

  private readonly bcanetItems = computed(() =>
    this.items().filter((x) => (x.loaiHaTang ?? 'KHAC') === 'BCANET'),
  );
  private readonly internetItems = computed(() =>
    this.items().filter((x) => (x.loaiHaTang ?? 'KHAC') === 'INTERNET'),
  );
  private readonly khacItems = computed(() =>
    this.items().filter((x) => !x.loaiHaTang || x.loaiHaTang === 'KHAC'),
  );

  readonly bcanetCount = computed(() => this.bcanetItems().length);
  readonly internetCount = computed(() => this.internetItems().length);
  readonly khacCount = computed(() => this.khacItems().length);

  readonly activeItems = computed(() => {
    const tab = this.activeTab();
    const filter = this.filterTenHttt().trim().toLowerCase();
    const base =
      tab === 'BCANET'
        ? this.bcanetItems()
        : tab === 'INTERNET'
          ? this.internetItems()
          : this.khacItems();
    if (!filter) return base;
    return base.filter((x) =>
      this.getTenHttt(x.htttId).toLowerCase().includes(filter),
    );
  });

  readonly form = this.fb.group({
    htttId: [null as number | null, Validators.required],
    loaiHaTang: ['BCANET' as string, Validators.required],
    chuQuan: [null as string | null],
    donViVanHanh: [null as string | null],
    capDoDeXuat: [null as string | null],
    tinhTrangPheDuyet: [null as string | null],
    quyetDinhPheDuyet: [null as string | null],
    quyCheAttt: [null as string | null],
    duKienNgayPheDuyet: [null as Date | null],
    daTrienKhaiPhuongAn: [false],
    duKienNgayTrienKhai: [null as Date | null],
    kiemTraDanhGia: [null as string | null],
    ghiChu: [null as string | null],
  });

  constructor(
    private readonly authService: AuthService,
    private readonly fb: FormBuilder,
    private readonly api: AtttHtttVanHanhApi,
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
      const [httt, items, donVi] = await Promise.all([
        this.heThongApi.getAll(),
        this.api.getAll(donViId || undefined),
        donViId ? this.donViApi.getById(donViId) : Promise.resolve(null),
      ]);

      this.htttCatalog.set(httt);
      this.items.set(items);

      if (donVi) {
        this.unitOptions.set(this.buildUnitOptions(donVi));
      }
    } finally {
      this.loading.set(false);
    }
  }

  async load(): Promise<void> {
    this.loading.set(true);
    try {
      const donViId = this.donViId();
      this.items.set(await this.api.getAll(donViId || undefined));
    } finally {
      this.loading.set(false);
    }
  }

  setActiveTab(tab: AtttTabKey): void {
    this.activeTab.set(tab);
    this.filterTenHttt.set('');
  }

  openDialog(item?: AtttHtttVanHanhDto): void {
    if (item) {
      this.selectedId.set(item.id);
      this.form.patchValue({
        htttId: item.htttId,
        loaiHaTang: item.loaiHaTang ?? this.activeTab(),
        chuQuan: item.chuQuan,
        donViVanHanh: item.donViVanHanh,
        capDoDeXuat: item.capDoDeXuat,
        tinhTrangPheDuyet: item.tinhTrangPheDuyet,
        quyetDinhPheDuyet: item.quyetDinhPheDuyet,
        quyCheAttt: item.quyCheAttt,
        duKienNgayPheDuyet: this.parseDate(item.duKienNgayPheDuyet),
        daTrienKhaiPhuongAn: item.daTrienKhaiPhuongAn,
        duKienNgayTrienKhai: this.parseDate(item.duKienNgayTrienKhai),
        kiemTraDanhGia: item.kiemTraDanhGia,
        ghiChu: item.ghiChu,
      });
    } else {
      this.selectedId.set(null);
      this.form.reset({
        loaiHaTang: this.activeTab(),
        daTrienKhaiPhuongAn: false,
      });
    }
    this.form.markAsPristine();
    this.form.markAsUntouched();
    this.dialogVisible.set(true);
  }

  closeDialog(): void {
    this.dialogVisible.set(false);
  }

  async save(): Promise<void> {
    if (this.form.invalid || this.saving()) {
      this.form.markAllAsTouched();
      return;
    }
    this.saving.set(true);
    try {
      const raw = this.form.getRawValue();
      const donViId = this.donViId();

      const payload: UpsertAtttHtttVanHanhRequest = {
        donViId,
        htttId: raw.htttId!,
        loaiHaTang: raw.loaiHaTang ?? null,
        chuQuan: raw.chuQuan?.trim() || null,
        donViVanHanh: raw.donViVanHanh?.trim() || null,
        capDoDeXuat: raw.capDoDeXuat?.trim() || null,
        tinhTrangPheDuyet: raw.tinhTrangPheDuyet?.trim() || null,
        quyetDinhPheDuyet: raw.quyetDinhPheDuyet?.trim() || null,
        quyCheAttt: raw.quyCheAttt?.trim() || null,
        duKienNgayPheDuyet: this.formatDate(raw.duKienNgayPheDuyet),
        daTrienKhaiPhuongAn: raw.daTrienKhaiPhuongAn ?? false,
        duKienNgayTrienKhai: this.formatDate(raw.duKienNgayTrienKhai),
        kiemTraDanhGia: raw.kiemTraDanhGia?.trim() || null,
        ghiChu: raw.ghiChu?.trim() || null,
      };

      const id = this.selectedId();
      if (id !== null) {
        await this.api.update(id, payload);
        this.notificationService.show('success', 'Cập nhật ATTT HTTT vận hành thành công.');
      } else {
        await this.api.create(payload);
        this.notificationService.show('success', 'Thêm mới ATTT HTTT vận hành thành công.');
      }

      this.closeDialog();
      await this.load();
    } finally {
      this.saving.set(false);
    }
  }

  async remove(item: AtttHtttVanHanhDto): Promise<void> {
    const tenHttt = this.getTenHttt(item.htttId);
    const confirmed = await this.confirmDialog.confirmDelete({
      message: `Xác nhận xóa bản ghi ATTT cho "${tenHttt}"?`,
      acceptLabel: 'Xóa',
      rejectLabel: 'Hủy',
    });
    if (!confirmed) return;

    await this.api.delete(item.id);
    this.notificationService.show('success', 'Xóa bản ghi ATTT thành công.');
    await this.load();
  }

  getTenHttt(htttId: number): string {
    return this.htttCatalog().find((h) => h.id === htttId)?.tenPhanMem ?? `HTTT #${htttId}`;
  }

  resolveLoaiHaTang(loaiHaTang: string | null): string {
    return LOAI_HA_TANG_OPTIONS.find((o) => o.value === loaiHaTang)?.label ?? loaiHaTang ?? '—';
  }

  resolveCapDo(capDo: string | null): string {
    return CAP_DO_OPTIONS.find((o) => o.value === capDo)?.label ?? capDo ?? '—';
  }

  resolveTinhTrang(tinhTrang: string | null): string {
    return TINH_TRANG_OPTIONS.find((o) => o.value === tinhTrang)?.label ?? tinhTrang ?? '—';
  }

  private buildUnitOptions(donVi: DonViDto): SelectOption[] {
    const options: SelectOption[] = [{ label: donVi.tenDonVi, value: donVi.tenDonVi }];
    for (const child of donVi.children) {
      options.push({ label: child.tenDonVi, value: child.tenDonVi });
    }
    return options;
  }

  private parseDate(value: string | null | undefined): Date | null {
    if (!value) return null;
    const d = new Date(value);
    return isNaN(d.getTime()) ? null : d;
  }

  private formatDate(date: Date | null | undefined): string | null {
    if (!date) return null;
    const d = new Date(date);
    const y = d.getFullYear();
    const m = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    return `${y}-${m}-${day}`;
  }
}
