import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
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
import { LoadingOverlayComponent } from '../../shared/ui/loading-overlay.component';
import { SectionCardComponent } from '../../shared/ui/section-card.component';
import { StatusBadgeComponent } from '../../shared/ui/status-badge.component';
import {
  MauBaoCaoApi,
  MauBaoCaoDto,
  TanSuat,
} from '../mau-bao-cao/mau-bao-cao.api';
import {
  CreateKyBaoCaoRequest,
  KyBaoCaoApi,
  KyBaoCaoDto,
  UpdateKyBaoCaoRequest,
} from './ky-bao-cao.api';

interface EditKyModel {
  tenKy: string;
  ngayKetThuc: Date | null;
  ghiChu: string;
}

interface CreateKyModel {
  mauBaoCaoId: number | null;
  nam: number;
  quy: number | null;
  thang: number | null;
  ngayKetThuc: Date | null;
  ghiChu: string;
  tenKy: string;
}

@Component({
  selector: 'app-ky-bao-cao-page',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    SectionCardComponent,
    StatusBadgeComponent,
    EmptyStateComponent,
    LoadingOverlayComponent,
    ButtonModule,
    CalendarModule,
    DialogModule,
    DropdownModule,
    InputTextModule,
    InputTextareaModule,
    TableModule,
    TooltipModule,
  ],
  templateUrl: './ky-bao-cao.page.html',
  styleUrl: './ky-bao-cao.page.scss',
})
export class KyBaoCaoPage {
  today = new Date();
  items: KyBaoCaoDto[] = [];

  filterStatus: number[] = [1, 2, 3];
  filterMauBaoCaoId: number | null = null;
  filterNam: number | null = new Date().getFullYear();
  filterQuaHan: boolean | null = null;
  filterTen = '';
  mauBaoCaos: MauBaoCaoDto[] = [];
  loading = false;
  creating = false;
  createError = '';
  showCreateDialog = false;

  showEditDialog = false;
  editing = false;
  editError = '';
  editingItem: KyBaoCaoDto | null = null;
  editModel: EditKyModel = { tenKy: '', ngayKetThuc: null, ghiChu: '' };

  showLockDialog = false;
  lockingItem: KyBaoCaoDto | null = null;
  lockCountdown = 5;
  private lockTimer: ReturnType<typeof setInterval> | null = null;
  private lastAutoTenKy = '';

  tienDoMap: Record<
    number,
    {
      chuaNhap: number;
      dangNhap: number;
      daXacNhan: number;
      dangBoSung: number;
      daNop: number;
      tong: number;
    }
  > = {};

  statusTextMap: Record<number, string> = {
    1: 'Chuẩn bị',
    2: 'Đang mở',
    3: 'Đã đóng',
    4: 'Khóa',
  };

  readonly quyOptions = [
    { label: 'Quý 1', value: 1 },
    { label: 'Quý 2', value: 2 },
    { label: 'Quý 3', value: 3 },
    { label: 'Quý 4', value: 4 },
  ];

  readonly thangOptions = Array.from({ length: 12 }, (_, index) => ({
    label: `Tháng ${index + 1}`,
    value: index + 1,
  }));

  createModel: CreateKyModel = this.buildDefaultCreateModel();

  constructor(
    private readonly authService: AuthService,
    private readonly kyBaoCaoApi: KyBaoCaoApi,
    private readonly mauBaoCaoApi: MauBaoCaoApi,
    private readonly notificationService: NotificationService,
    private readonly confirmDialog: ConfirmDialogWrapperService,
  ) {
    void this.load();
  }

  get filteredItems(): KyBaoCaoDto[] {
    return this.items.filter((item) => {
      if (
        this.filterStatus.length > 0 &&
        !this.filterStatus.includes(item.trangThai)
      )
        return false;
      if (
        this.filterMauBaoCaoId !== null &&
        item.mauBaoCaoId !== this.filterMauBaoCaoId
      )
        return false;
      if (this.filterNam !== null && item.nam !== this.filterNam) return false;
      if (this.filterQuaHan !== null) {
        const overdue = item.ngayKetThuc
          ? new Date(item.ngayKetThuc) < this.today
          : false;
        if (this.filterQuaHan !== overdue) return false;
      }
      if (this.filterTen) {
        const q = this.filterTen.toLowerCase();
        const name = (item.tenKy || this.tenKy(item)).toLowerCase();
        if (!name.includes(q) && !item.kyCode.toLowerCase().includes(q))
          return false;
      }
      return true;
    });
  }

  get namOptions(): { label: string; value: number }[] {
    const years = [...new Set(this.items.map((x) => x.nam))].sort(
      (a, b) => b - a,
    );
    return years.map((y) => ({ label: String(y), value: y }));
  }

  private readonly defaultStatus = [1, 2, 3];
  private readonly defaultNam = new Date().getFullYear();

  get hasActiveFilter(): boolean {
    const statusChanged =
      this.filterStatus.length !== this.defaultStatus.length ||
      !this.defaultStatus.every((s) => this.filterStatus.includes(s));
    return (
      statusChanged ||
      this.filterMauBaoCaoId !== null ||
      this.filterQuaHan !== null ||
      !!this.filterTen
    );
  }

  countByStatus(status: number): number {
    // Count items matching all currently active filters EXCEPT the status chips
    return this.items.filter((item) => {
      if (item.trangThai !== status) return false;
      if (
        this.filterMauBaoCaoId !== null &&
        item.mauBaoCaoId !== this.filterMauBaoCaoId
      )
        return false;
      if (this.filterNam !== null && item.nam !== this.filterNam) return false;
      if (this.filterTen) {
        const q = this.filterTen.toLowerCase();
        const name = (item.tenKy || this.tenKy(item)).toLowerCase();
        if (!name.includes(q) && !item.kyCode.toLowerCase().includes(q))
          return false;
      }
      return true;
    }).length;
  }

  isStatusSelected(status: number): boolean {
    return this.filterStatus.includes(status);
  }

  toggleStatus(status: number): void {
    if (this.isStatusSelected(status)) {
      this.filterStatus = this.filterStatus.filter((s) => s !== status);
    } else {
      this.filterStatus = [...this.filterStatus, status];
    }
  }

  resetFilters(): void {
    this.filterStatus = [...this.defaultStatus];
    this.filterMauBaoCaoId = null;
    this.filterNam = this.defaultNam;
    this.filterQuaHan = null;
    this.filterTen = '';
  }

  get selectedMauBaoCao(): MauBaoCaoDto | null {
    if (!this.createModel.mauBaoCaoId) {
      return null;
    }

    return (
      this.mauBaoCaos.find(
        (item) => item.id === this.createModel.mauBaoCaoId,
      ) ?? null
    );
  }

  get selectedTanSuat(): TanSuat | null {
    return this.selectedMauBaoCao?.tanSuat ?? null;
  }

  get showQuyField(): boolean {
    return this.selectedTanSuat === 2;
  }

  get showThangField(): boolean {
    return this.selectedTanSuat === 1;
  }

  openCreateDialog(): void {
    this.createError = '';
    this.resetCreateModel();
    this.showCreateDialog = true;
  }

  closeCreateDialog(): void {
    this.showCreateDialog = false;
  }

  canEdit(item: KyBaoCaoDto): boolean {
    return (
      this.authService.hasPermission('ky_bao_cao:update') &&
      (item.trangThai === 1 || item.trangThai === 2)
    );
  }

  canDelete(item: KyBaoCaoDto): boolean {
    return (
      this.authService.hasPermission('ky_bao_cao:delete') &&
      item.trangThai === 1
    );
  }

  openEditDialog(item: KyBaoCaoDto): void {
    this.editingItem = item;
    this.editError = '';
    this.editModel = {
      tenKy: item.tenKy || this.tenKy(item),
      ngayKetThuc: item.ngayKetThuc ? new Date(item.ngayKetThuc) : null,
      ghiChu: item.ghiChu || '',
    };
    this.showEditDialog = true;
  }

  closeEditDialog(): void {
    this.showEditDialog = false;
    this.editingItem = null;
  }

  async saveEdit(): Promise<void> {
    if (!this.editingItem) return;
    if (!this.editModel.tenKy?.trim()) {
      this.editError = 'Vui lòng nhập tên kỳ báo cáo.';
      return;
    }
    this.editing = true;
    this.editError = '';
    try {
      const d = this.editModel.ngayKetThuc;
      const payload: UpdateKyBaoCaoRequest = {
        tenKy: this.editModel.tenKy.trim(),
        ngayKetThuc: d
          ? `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
          : undefined,
        ghiChu: this.editModel.ghiChu?.trim() || undefined,
      };
      await this.kyBaoCaoApi.update(this.editingItem.id, payload);
      this.notificationService.show('success', 'Đã cập nhật kỳ báo cáo.');
      this.closeEditDialog();
      await this.load();
    } catch (error: unknown) {
      this.editError = this.extractError(
        error,
        'Không thể cập nhật kỳ báo cáo.',
      );
    } finally {
      this.editing = false;
    }
  }

  async deleteKy(item: KyBaoCaoDto): Promise<void> {
    const confirmed = await this.confirmDialog.confirmSubmit({
      header: 'Xóa kỳ báo cáo',
      message: `Xác nhận xóa kỳ "${item.tenKy || item.kyCode}"? Thao tác không thể hoàn tác.`,
      acceptLabel: 'Xóa',
      rejectLabel: 'Hủy',
    });
    if (!confirmed) return;
    try {
      await this.kyBaoCaoApi.delete(item.id);
      this.notificationService.show('success', `Đã xóa kỳ ${item.kyCode}.`);
      await this.load();
    } catch (error: unknown) {
      this.notificationService.show('error', this.extractError(error));
    }
  }

  async moLaiKy(item: KyBaoCaoDto): Promise<void> {
    await this.changeStatus(
      item,
      2,
      'Mở lại kỳ báo cáo',
      `Xác nhận mở lại kỳ ${item.kyCode}? Các đơn vị sẽ có thể tiếp tục nộp báo cáo.`,
      'Mở lại',
      `Đã mở lại kỳ ${item.kyCode}.`,
    );
  }

  async load(): Promise<void> {
    this.loading = true;
    this.tienDoMap = {};

    try {
      const [items, mauBaoCaos] = await Promise.all([
        this.kyBaoCaoApi.getAll(),
        this.mauBaoCaoApi.getAll(false),
      ]);

      this.items = items;
      this.mauBaoCaos = mauBaoCaos;
      this.resetCreateModel();
    } catch (error: unknown) {
      this.items = [];
      this.notificationService.show('error', this.extractError(error));
    } finally {
      this.loading = false;
    }

    void this.loadTienDoAll();
  }

  async loadTienDoAll(): Promise<void> {
    const activeKy = this.items.filter((item) => item.trangThai <= 3);
    if (activeKy.length === 0) {
      this.tienDoMap = {};
      return;
    }

    const results = await Promise.allSettled(
      activeKy.map((item) => this.kyBaoCaoApi.getTienDo(item.id)),
    );

    this.tienDoMap = {};
    results.forEach((result, index) => {
      if (result.status === 'fulfilled') {
        const td = result.value;
        this.tienDoMap[activeKy[index].id] = {
          chuaNhap: td.soDonViChuaNhap,
          dangNhap: td.soDonViDangNhap,
          daXacNhan: td.soDonViDaXacNhan,
          dangBoSung: td.soDonViDangBoSung,
          daNop: td.soDonViDaNop,
          tong: td.tongDonVi,
        };
      }
    });
  }

  tienDoPercent(id: number): number {
    const td = this.tienDoMap[id];
    if (!td || td.tong === 0) return 0;
    return Math.round((td.daNop / td.tong) * 100);
  }

  suggestTenKy(): string {
    const mau = this.selectedMauBaoCao;
    if (!mau) return '';
    const nam = this.createModel.nam;
    if (this.showQuyField && this.createModel.quy) {
      return `${mau.tenMau} Quý ${this.createModel.quy} Năm ${nam}`;
    }
    if (this.showThangField && this.createModel.thang) {
      return `${mau.tenMau} Tháng ${this.createModel.thang} Năm ${nam}`;
    }
    return `${mau.tenMau} Năm ${nam}`;
  }

  applyAutoTenKy(): void {
    const suggested = this.suggestTenKy();
    if (
      !this.createModel.tenKy ||
      this.createModel.tenKy === this.lastAutoTenKy
    ) {
      this.createModel.tenKy = suggested;
      this.lastAutoTenKy = suggested;
    }
  }

  onMauBaoCaoChange(): void {
    const now = new Date();
    this.createError = '';
    if (this.showQuyField) {
      this.createModel.thang = null;
      this.applyAutoTenKy();
      return;
    }

    if (this.showThangField) {
      this.createModel.quy = null;
      this.createModel.thang = now.getMonth() + 1;
      this.applyAutoTenKy();
      return;
    }

    this.createModel.quy = null;
    this.createModel.thang = null;
    this.applyAutoTenKy();
  }

  async createKyBaoCao(): Promise<void> {
    if (!this.createModel.mauBaoCaoId) {
      this.createError = 'Vui lòng chọn mẫu báo cáo.';
      return;
    }

    if (!this.createModel.tenKy?.trim()) {
      this.createError = 'Vui lòng nhập tên kỳ báo cáo.';
      return;
    }

    if (!this.createModel.ngayKetThuc) {
      this.createError = 'Vui lòng chọn hạn nộp báo cáo.';
      return;
    }

    this.creating = true;
    this.createError = '';

    try {
      const dueDate = this.createModel.ngayKetThuc;
      const ngayKetThuc = `${dueDate.getFullYear()}-${String(
        dueDate.getMonth() + 1,
      ).padStart(2, '0')}-${String(dueDate.getDate()).padStart(2, '0')}`;

      const payload: CreateKyBaoCaoRequest = {
        mauBaoCaoId: this.createModel.mauBaoCaoId,
        nam: this.createModel.nam,
        quy: this.showQuyField
          ? (this.createModel.quy ?? undefined)
          : undefined,
        thang: this.showThangField
          ? (this.createModel.thang ?? undefined)
          : undefined,
        ngayBatDau: new Date().toISOString().split('T')[0],
        ngayKetThuc,
        ghiChu: this.createModel.ghiChu?.trim() || undefined,
        tenKy: this.createModel.tenKy.trim(),
      };

      const created = await this.kyBaoCaoApi.create(payload);

      try {
        await this.kyBaoCaoApi.updateStatus(created.id, {
          trangThai: 2,
          ghiChu: 'Tự động mở khi tạo',
        });
        this.notificationService.show(
          'success',
          `Đã tạo và mở kỳ báo cáo ${created.kyCode}.`,
        );
      } catch {
        this.notificationService.show(
          'warning',
          `Đã tạo kỳ ${created.kyCode} nhưng chưa mở được — vui lòng mở thủ công.`,
        );
      }

      this.closeCreateDialog();
      await this.load();
    } catch (error: unknown) {
      this.createError = this.extractError(error, 'Không thể tạo kỳ báo cáo.');
    } finally {
      this.creating = false;
    }
  }

  formatDate(value: string | null | undefined): string {
    if (!value) {
      return '—';
    }

    const date = new Date(value);
    return `${String(date.getDate()).padStart(2, '0')}/${String(
      date.getMonth() + 1,
    ).padStart(2, '0')}/${date.getFullYear()}`;
  }

  isOverDeadline(value: string | null | undefined): boolean {
    if (!value) {
      return false;
    }

    return new Date(value) < this.today;
  }

  statusText(status: number): string {
    return this.statusTextMap[status] ?? 'Không xác định';
  }

  severityFor(status: number): 'contrast' | 'warning' | 'success' | 'danger' {
    if (status === 2) {
      return 'success';
    }
    if (status === 3) {
      return 'warning';
    }
    if (status === 4) {
      return 'danger';
    }

    return 'contrast';
  }

  badgeTone(status: number): 'neutral' | 'warning' | 'success' | 'danger' {
    const severity = this.severityFor(status);
    return severity === 'contrast' ? 'neutral' : severity;
  }

  async moKy(item: KyBaoCaoDto): Promise<void> {
    await this.changeStatus(
      item,
      2,
      'Mở kỳ báo cáo',
      `Xác nhận mở kỳ ${item.kyCode}? Các đơn vị sẽ có thể nộp báo cáo.`,
      'Mở kỳ',
      `Đã mở kỳ ${item.kyCode}.`,
    );
  }

  async dongKy(item: KyBaoCaoDto): Promise<void> {
    await this.changeStatus(
      item,
      3,
      'Đóng kỳ báo cáo',
      `Xác nhận đóng kỳ ${item.kyCode}? Sau khi đóng các đơn vị không thể nộp thêm.`,
      'Đóng kỳ',
      `Đã đóng kỳ ${item.kyCode}.`,
    );
  }

  khoaKy(item: KyBaoCaoDto): void {
    this.lockingItem = item;
    this.lockCountdown = 5;
    this.showLockDialog = true;
    this.lockTimer = setInterval(() => {
      this.lockCountdown--;
      if (this.lockCountdown <= 0) {
        this.lockCountdown = 0;
        if (this.lockTimer) {
          clearInterval(this.lockTimer);
          this.lockTimer = null;
        }
      }
    }, 1000);
  }

  cancelLockDialog(): void {
    this.showLockDialog = false;
    this.lockingItem = null;
    if (this.lockTimer) {
      clearInterval(this.lockTimer);
      this.lockTimer = null;
    }
  }

  async confirmLockKy(): Promise<void> {
    if (!this.lockingItem || this.lockCountdown > 0) return;
    const item = this.lockingItem;
    this.cancelLockDialog();
    try {
      await this.kyBaoCaoApi.updateStatus(item.id, {
        trangThai: 4,
        ghiChu: 'Khóa kỳ từ giao diện',
      });
      this.notificationService.show('success', `Đã khóa kỳ ${item.kyCode}.`);
      await this.load();
    } catch (error: unknown) {
      this.notificationService.show('error', this.extractError(error));
    }
  }

  kyChuKyText(item: KyBaoCaoDto): string {
    if (item.quy) {
      return `Q${item.quy}`;
    }

    if (item.thang) {
      return `T${item.thang.toString().padStart(2, '0')}`;
    }

    return 'Năm';
  }

  tenKy(item: KyBaoCaoDto): string {
    return `${this.mauBaoCaoName(item.mauBaoCaoId)} ${this.kyChuKyText(item)} ${item.nam}`;
  }

  mauBaoCaoName(id?: number): string {
    if (!id) {
      return '-';
    }

    const item = this.mauBaoCaos.find((x) => x.id === id);
    return item ? item.tenMau : `#${id}`;
  }

  tanSuatLabel(tanSuat: number): string {
    const map: Record<number, string> = {
      0: 'Hàng năm',
      1: 'Hàng tháng',
      2: 'Hàng quý',
      3: 'Hàng năm',
    };

    return map[tanSuat] ?? '';
  }

  private async changeStatus(
    item: KyBaoCaoDto,
    targetStatus: number,
    header: string,
    message: string,
    acceptLabel: string,
    successMessage: string,
  ): Promise<void> {
    const confirmed = await this.confirmDialog.confirmSubmit({
      header,
      message,
      acceptLabel,
      rejectLabel: 'Hủy',
    });

    if (!confirmed) {
      return;
    }

    try {
      await this.kyBaoCaoApi.updateStatus(item.id, {
        trangThai: targetStatus,
        ghiChu: `${acceptLabel} từ giao diện`,
      });
      this.notificationService.show('success', successMessage);
      await this.load();
    } catch (error: unknown) {
      this.notificationService.show('error', this.extractError(error));
    }
  }

  private resetCreateModel(): void {
    const first = this.mauBaoCaos[0] ?? null;
    this.lastAutoTenKy = '';
    this.createModel = {
      ...this.buildDefaultCreateModel(),
      mauBaoCaoId: first?.id ?? null,
    };
    this.onMauBaoCaoChange();
  }

  private buildDefaultCreateModel(): CreateKyModel {
    const now = new Date();
    const month = now.getMonth() + 1;
    const currentQuy = month <= 3 ? 1 : month <= 6 ? 2 : month <= 9 ? 3 : 4;

    return {
      mauBaoCaoId: null,
      nam: now.getFullYear(),
      quy: currentQuy,
      thang: null,
      ngayKetThuc: null,
      ghiChu: '',
      tenKy: '',
    };
  }

  private extractError(
    error: unknown,
    fallback = 'Thao tác thất bại.',
  ): string {
    const responseError = (
      error as {
        error?: {
          error?: { message?: string; Message?: string };
          Error?: { message?: string; Message?: string };
        };
      }
    )?.error;

    return (
      responseError?.error?.message ??
      responseError?.error?.Message ??
      responseError?.Error?.message ??
      responseError?.Error?.Message ??
      fallback
    );
  }
}
