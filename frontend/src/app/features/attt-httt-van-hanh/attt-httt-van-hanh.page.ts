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
import { TongHopModeBannerComponent } from '../../shared/ui/tong-hop-mode-banner.component';
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

const TRANG_THAI_TRIEN_KHAI_OPTIONS: SelectOption[] = [
  { label: 'Chưa triển khai', value: 'CHUA_TRIEN_KHAI' },
  { label: 'Đang triển khai', value: 'DANG_TRIEN_KHAI' },
  { label: 'Đã triển khai đầy đủ', value: 'DA_TRIEN_KHAI_DAY_DU' },
];

@Component({
  selector: 'app-attt-httt-van-hanh-page',
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
    DialogModule,
    DropdownModule,
    CalendarModule,
    InputTextModule,
    InputTextareaModule,
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
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly dialogVisible = signal(false);
  readonly selectedId = signal<number | null>(null);
  readonly activeTab = signal<AtttTabKey>('BCANET');
  readonly filterTenHttt = signal('');

  readonly capDoOptions = CAP_DO_OPTIONS;
  readonly tinhTrangOptions = TINH_TRANG_OPTIONS;
  readonly loaiHaTangOptions = LOAI_HA_TANG_OPTIONS;
  readonly trangThaiTrienKhaiOptions = TRANG_THAI_TRIEN_KHAI_OPTIONS;

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
    chuQuan: [null as string | null, Validators.required],
    donViVanHanh: [null as string | null, Validators.required],
    capDoDeXuat: [null as string | null, Validators.required],
    tinhTrangPheDuyet: [null as string | null, Validators.required],
    quyetDinhPheDuyet: [null as string | null, Validators.maxLength(200)],
    quyCheAttt: [null as string | null, Validators.maxLength(200)],
    duKienNgayPheDuyet: [null as Date | null],
    trangThaiTrienKhaiPhuongAn: ['CHUA_TRIEN_KHAI' as string, Validators.required],
    noiDungPhuongAnDaTrienKhai: [null as string | null, Validators.maxLength(2000)],
    duKienNgayTrienKhai: [null as Date | null],
    kiemTraDanhGia: [null as string | null, Validators.maxLength(500)],
    ghiChu: [null as string | null, Validators.maxLength(2000)],
  });

  private readonly unitNames = new Map<number, string>();

  constructor(
    private readonly authService: AuthService,
    private readonly fb: FormBuilder,
    private readonly api: AtttHtttVanHanhApi,
    private readonly heThongApi: HeThongThongTinApi,
    private readonly donViApi: DonViApi,
    private readonly notificationService: NotificationService,
    private readonly confirmDialog: ConfirmDialogWrapperService,
  ) {
    this.form.controls.tinhTrangPheDuyet.valueChanges.subscribe(() => this.updateConditionalValidators());
    this.form.controls.trangThaiTrienKhaiPhuongAn.valueChanges.subscribe(() => this.updateConditionalValidators());
    this.form.controls.htttId.valueChanges.subscribe((htttId) => this.syncLinkedSystemUnits(htttId));
    this.updateConditionalValidators();
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
        this.indexUnitNames(donVi);
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
        capDoDeXuat: item.capDoDeXuat,
        tinhTrangPheDuyet: item.tinhTrangPheDuyet,
        quyetDinhPheDuyet: item.quyetDinhPheDuyet,
        quyCheAttt: item.quyCheAttt,
        duKienNgayPheDuyet: this.parseDate(item.duKienNgayPheDuyet),
        trangThaiTrienKhaiPhuongAn: item.trangThaiTrienKhaiPhuongAn
          ?? (item.daTrienKhaiPhuongAn ? 'DA_TRIEN_KHAI_DAY_DU' : 'CHUA_TRIEN_KHAI'),
        noiDungPhuongAnDaTrienKhai: item.noiDungPhuongAnDaTrienKhai,
        duKienNgayTrienKhai: this.parseDate(item.duKienNgayTrienKhai),
        kiemTraDanhGia: item.kiemTraDanhGia,
        ghiChu: item.ghiChu,
      });
      this.syncLinkedSystemUnits(item.htttId);
    } else {
      this.selectedId.set(null);
      this.form.reset({
        loaiHaTang: this.activeTab(),
        trangThaiTrienKhaiPhuongAn: 'CHUA_TRIEN_KHAI',
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
        daTrienKhaiPhuongAn: raw.trangThaiTrienKhaiPhuongAn === 'DA_TRIEN_KHAI_DAY_DU',
        trangThaiTrienKhaiPhuongAn: raw.trangThaiTrienKhaiPhuongAn ?? null,
        noiDungPhuongAnDaTrienKhai: raw.noiDungPhuongAnDaTrienKhai?.trim() || null,
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

  resolveTrangThaiTrienKhai(trangThai: string | null, legacyCompleted = false): string {
    const effective = trangThai ?? (legacyCompleted ? 'DA_TRIEN_KHAI_DAY_DU' : 'CHUA_TRIEN_KHAI');
    return TRANG_THAI_TRIEN_KHAI_OPTIONS.find((o) => o.value === effective)?.label ?? effective;
  }

  getChuQuanHttt(htttId: number, fallback?: string | null): string {
    const system = this.htttCatalog().find((item) => item.id === htttId);
    return (system ? this.unitNames.get(system.donViId) : null) ?? fallback ?? '—';
  }

  getDonViVanHanhHttt(htttId: number, fallback?: string | null): string {
    return this.htttCatalog().find((item) => item.id === htttId)?.donViQuanLy?.trim() || fallback || '—';
  }

  formatDisplayDate(value: string | null): string {
    if (!value) return '—';
    const [year, month, day] = value.split('-');
    return year && month && day ? `${day}/${month}/${year}` : value;
  }

  isApproved(): boolean {
    return this.form.controls.tinhTrangPheDuyet.value === 'DA_PHE_DUYET';
  }

  isDeploymentComplete(): boolean {
    return this.form.controls.trangThaiTrienKhaiPhuongAn.value === 'DA_TRIEN_KHAI_DAY_DU';
  }

  requiresDeploymentDetails(): boolean {
    const value = this.form.controls.trangThaiTrienKhaiPhuongAn.value;
    return value === 'DANG_TRIEN_KHAI' || value === 'DA_TRIEN_KHAI_DAY_DU';
  }

  private indexUnitNames(donVi: DonViDto): void {
    this.unitNames.set(donVi.id, donVi.tenDonVi);
    for (const child of donVi.children ?? []) this.indexUnitNames(child);
  }

  private syncLinkedSystemUnits(htttId: number | null): void {
    const system = htttId == null
      ? null
      : this.htttCatalog().find((item) => item.id === htttId);
    this.form.patchValue({
      chuQuan: system ? (this.unitNames.get(system.donViId) ?? null) : null,
      donViVanHanh: system?.donViQuanLy?.trim() || null,
    }, { emitEvent: false });
  }

  private updateConditionalValidators(): void {
    const decision = this.form.controls.quyetDinhPheDuyet;
    const approvalDate = this.form.controls.duKienNgayPheDuyet;
    const deploymentDetails = this.form.controls.noiDungPhuongAnDaTrienKhai;
    const deploymentDate = this.form.controls.duKienNgayTrienKhai;

    decision.setValidators(this.isApproved()
      ? [Validators.required, Validators.maxLength(200)]
      : [Validators.maxLength(200)]);
    approvalDate.setValidators(this.isApproved() ? [] : [Validators.required]);
    deploymentDetails.setValidators(this.requiresDeploymentDetails()
      ? [Validators.required, Validators.maxLength(2000)]
      : [Validators.maxLength(2000)]);
    deploymentDate.setValidators(this.isDeploymentComplete() ? [] : [Validators.required]);

    decision.updateValueAndValidity({ emitEvent: false });
    approvalDate.updateValueAndValidity({ emitEvent: false });
    deploymentDetails.updateValueAndValidity({ emitEvent: false });
    deploymentDate.updateValueAndValidity({ emitEvent: false });
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
