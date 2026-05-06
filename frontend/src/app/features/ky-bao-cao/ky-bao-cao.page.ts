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
} from './ky-bao-cao.api';

interface CreateKyModel {
  mauBaoCaoId: number | null;
  nam: number;
  quy: number | null;
  thang: number | null;
  ngayKetThuc: Date | null;
  ghiChu: string;
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
  current: KyBaoCaoDto | null = null;
  items: KyBaoCaoDto[] = [];
  mauBaoCaos: MauBaoCaoDto[] = [];
  loading = false;
  creating = false;
  createError = '';
  showCreateDialog = false;
  tienDoMap: Record<number, { daNop: number; tong: number }> = {};

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
    private readonly kyBaoCaoApi: KyBaoCaoApi,
    private readonly mauBaoCaoApi: MauBaoCaoApi,
    private readonly notificationService: NotificationService,
    private readonly confirmDialog: ConfirmDialogWrapperService,
  ) {
    void this.load();
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

  async load(): Promise<void> {
    this.loading = true;
    this.tienDoMap = {};

    try {
      const [current, items, mauBaoCaos] = await Promise.all([
        this.kyBaoCaoApi.getCurrent().catch(() => null),
        this.kyBaoCaoApi.getAll(),
        this.mauBaoCaoApi.getAll(false),
      ]);

      this.current = current;
      this.items = items;
      this.mauBaoCaos = mauBaoCaos;
      this.resetCreateModel();
    } catch (error: unknown) {
      this.current = null;
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
        const tienDo = result.value;
        this.tienDoMap[activeKy[index].id] = {
          daNop: tienDo.soDonViDaNop,
          tong: tienDo.tongDonVi,
        };
      }
    });
  }

  onMauBaoCaoChange(): void {
    const now = new Date();
    this.createError = '';
    if (this.showQuyField) {
      this.createModel.thang = null;
      return;
    }

    if (this.showThangField) {
      this.createModel.quy = null;
      this.createModel.thang = now.getMonth() + 1;
      return;
    }

    this.createModel.quy = null;
    this.createModel.thang = null;
  }

  async createKyBaoCao(): Promise<void> {
    if (!this.createModel.mauBaoCaoId) {
      this.createError = 'Vui lòng chọn mẫu báo cáo.';
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
      };

      const created = await this.kyBaoCaoApi.create(payload);

      await this.kyBaoCaoApi.updateStatus(created.id, {
        trangThai: 2,
        ghiChu: 'Tự động mở khi tạo',
      });

      this.notificationService.show(
        'success',
        `Đã tạo và mở kỳ báo cáo ${created.kyCode}.`,
      );
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

  async khoaKy(item: KyBaoCaoDto): Promise<void> {
    await this.changeStatus(
      item,
      4,
      'Khóa kỳ báo cáo',
      `Xác nhận khóa kỳ ${item.kyCode}? Dữ liệu sẽ được khóa hoàn toàn.`,
      'Khóa kỳ',
      `Đã khóa kỳ ${item.kyCode}.`,
    );
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
