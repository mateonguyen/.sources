import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { DropdownModule } from 'primeng/dropdown';
import { InputTextareaModule } from 'primeng/inputtextarea';
import { TableModule } from 'primeng/table';
import { AuthService } from '../../core/auth/auth.service';
import { NotificationService } from '../../core/ui/notification.service';
import { DonViApi } from '../don-vi/don-vi.api';
import { KyBaoCaoApi, KyBaoCaoDto } from '../ky-bao-cao/ky-bao-cao.api';
import { ConfirmDialogWrapperService } from '../../shared/ui/confirm-dialog-wrapper.service';
import { EmptyStateComponent } from '../../shared/ui/empty-state.component';
import { FilterBarComponent } from '../../shared/ui/filter-bar.component';
import { LoadingOverlayComponent } from '../../shared/ui/loading-overlay.component';
import { SectionCardComponent } from '../../shared/ui/section-card.component';
import { StatusBadgeComponent } from '../../shared/ui/status-badge.component';
import {
  CreateYeuCauBoSungRequest,
  YeuCauBoSungApi,
  YeuCauBoSungDto,
} from './yeu-cau-bo-sung.api';

interface DonViOption {
  label: string;
  value: number;
}

@Component({
  selector: 'app-yeu-cau-bo-sung-page',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    SectionCardComponent,
    FilterBarComponent,
    EmptyStateComponent,
    LoadingOverlayComponent,
    StatusBadgeComponent,
    DropdownModule,
    InputTextareaModule,
    ButtonModule,
    TableModule,
  ],
  templateUrl: './yeu-cau-bo-sung.page.html',
  styleUrl: './yeu-cau-bo-sung.page.scss',
})
export class YeuCauBoSungPage {
  loading = false;
  creating = false;
  processing = false;

  kyBaoCaos: KyBaoCaoDto[] = [];
  selectedKyBaoCaoId: number | null = null;
  selectedTrangThaiFilter: number | null = null;
  selectedDonViFilterId: number | null = null;

  donViOptions: DonViOption[] = [];
  requests: YeuCauBoSungDto[] = [];

  createModel = {
    donViId: null as number | null,
    lyDo: '',
    hanBoSung: '',
  };

  rejectTarget: YeuCauBoSungDto | null = null;
  rejectReason = '';

  constructor(
    private readonly yeuCauApi: YeuCauBoSungApi,
    private readonly kyBaoCaoApi: KyBaoCaoApi,
    private readonly donViApi: DonViApi,
    private readonly route: ActivatedRoute,
    private readonly authService: AuthService,
    private readonly notificationService: NotificationService,
    private readonly confirmDialog: ConfirmDialogWrapperService,
  ) {
    void this.loadInitialData();
  }

  get kyOptions(): Array<{ label: string; value: number }> {
    return this.kyBaoCaos.map((item) => ({
      label: `${item.kyCode} · ${this.kyStatusLabel(item.trangThai)}`,
      value: item.id,
    }));
  }

  get canCreate(): boolean {
    return this.authService.hasPermission('yeu_cau_bo_sung:create');
  }

  get canApprove(): boolean {
    return this.authService.hasPermission('yeu_cau_bo_sung:approve');
  }

  get canRead(): boolean {
    return this.authService.hasPermission('yeu_cau_bo_sung:read');
  }

  get trangThaiFilterOptions(): Array<{ label: string; value: number | null }> {
    return [
      { label: 'Tất cả trạng thái', value: null },
      { label: 'Chờ duyệt', value: 1 },
      { label: 'Đã duyệt', value: 2 },
      { label: 'Từ chối', value: 3 },
      { label: 'Đang bổ sung', value: 4 },
      { label: 'Hoàn thành', value: 5 },
    ];
  }

  get donViFilterOptions(): Array<{ label: string; value: number | null }> {
    return [{ label: 'Tất cả đơn vị', value: null }, ...this.donViOptions];
  }

  get filteredRequests(): YeuCauBoSungDto[] {
    return this.requests.filter((item) => {
      const matchTrangThai =
        this.selectedTrangThaiFilter == null ||
        item.trangThai === this.selectedTrangThaiFilter;
      const matchDonVi =
        this.selectedDonViFilterId == null ||
        item.donViId === this.selectedDonViFilterId;
      return matchTrangThai && matchDonVi;
    });
  }

  async loadInitialData(): Promise<void> {
    if (!this.canRead) {
      return;
    }

    this.loading = true;
    try {
      const [allKy, donViTree] = await Promise.all([
        this.kyBaoCaoApi.getAll(),
        this.donViApi.getTree(),
      ]);

      this.kyBaoCaos = allKy;
      const queryKyIdRaw = this.route.snapshot.queryParamMap.get('kyBaoCaoId');
      const queryKyId = queryKyIdRaw ? Number(queryKyIdRaw) : NaN;
      const hasQueryKy = Number.isInteger(queryKyId) && queryKyId > 0;
      const selectedFromQuery = hasQueryKy
        ? (allKy.find((item) => item.id === queryKyId)?.id ?? null)
        : null;

      this.selectedKyBaoCaoId = selectedFromQuery ?? allKy[0]?.id ?? null;
      this.donViOptions = this.flattenDonViOptions(donViTree);

      if (this.selectedKyBaoCaoId) {
        await this.loadRequests(this.selectedKyBaoCaoId);
      }
    } finally {
      this.loading = false;
    }
  }

  async onKyChange(value: number | null | undefined): Promise<void> {
    const nextValue = value ?? null;
    this.selectedKyBaoCaoId = nextValue;
    this.requests = [];
    this.rejectTarget = null;

    if (!nextValue) {
      return;
    }

    await this.loadRequests(nextValue);
  }

  async createRequest(): Promise<void> {
    if (!this.canCreate || !this.selectedKyBaoCaoId) {
      return;
    }

    const donViId = this.createModel.donViId ?? 0;
    const lyDo = this.createModel.lyDo.trim();
    if (!donViId || !lyDo) {
      this.notificationService.show(
        'warning',
        'Cần chọn đơn vị và nhập lý do bổ sung.',
      );
      return;
    }

    const payload: CreateYeuCauBoSungRequest = {
      kyBaoCaoId: this.selectedKyBaoCaoId,
      donViId,
      lyDo,
      hanBoSung: this.normalizeDateValue(this.createModel.hanBoSung),
    };

    this.creating = true;
    try {
      await this.yeuCauApi.create(payload);
      this.notificationService.show('success', 'Đã tạo yêu cầu bổ sung.');
      this.resetCreateForm();
      await this.loadRequests(this.selectedKyBaoCaoId);
    } catch (error: unknown) {
      this.notificationService.show('error', this.extractError(error));
    } finally {
      this.creating = false;
    }
  }

  async approve(item: YeuCauBoSungDto): Promise<void> {
    if (!this.canApprove || item.trangThai !== 1) {
      return;
    }

    const confirmed = await this.confirmDialog.confirmApprove({
      header: 'Duyệt yêu cầu bổ sung',
      message: `Xác nhận duyệt yêu cầu #${item.id} để mở lại snapshot cho đơn vị #${item.donViId}?`,
      acceptLabel: 'Duyệt',
    });

    if (!confirmed || !this.selectedKyBaoCaoId) {
      return;
    }

    this.processing = true;
    try {
      await this.yeuCauApi.duyet(item.id, {
        hanBoSung: item.hanBoSung,
      });
      this.notificationService.show(
        'success',
        'Đã duyệt yêu cầu và mở lại snapshot.',
      );
      await this.loadRequests(this.selectedKyBaoCaoId);
    } catch (error: unknown) {
      this.notificationService.show('error', this.extractError(error));
    } finally {
      this.processing = false;
    }
  }

  openReject(item: YeuCauBoSungDto): void {
    if (!this.canApprove || item.trangThai !== 1) {
      return;
    }

    this.rejectTarget = item;
    this.rejectReason = '';
  }

  cancelReject(): void {
    this.rejectTarget = null;
    this.rejectReason = '';
  }

  async submitReject(): Promise<void> {
    if (!this.rejectTarget || !this.selectedKyBaoCaoId) {
      return;
    }

    const reason = this.rejectReason.trim();
    if (!reason) {
      this.notificationService.show('warning', 'Cần nhập lý do từ chối.');
      return;
    }

    const confirmed = await this.confirmDialog.confirmReject({
      header: 'Từ chối yêu cầu bổ sung',
      message: `Xác nhận từ chối yêu cầu #${this.rejectTarget.id}?`,
      acceptLabel: 'Từ chối',
    });

    if (!confirmed) {
      return;
    }

    this.processing = true;
    try {
      await this.yeuCauApi.tuChoi(this.rejectTarget.id, {
        tuChoiLyDo: reason,
      });
      this.notificationService.show('success', 'Đã từ chối yêu cầu bổ sung.');
      this.cancelReject();
      await this.loadRequests(this.selectedKyBaoCaoId);
    } catch (error: unknown) {
      this.notificationService.show('error', this.extractError(error));
    } finally {
      this.processing = false;
    }
  }

  statusLabel(status: number): string {
    switch (status) {
      case 1:
        return 'Chờ duyệt';
      case 2:
        return 'Đã duyệt';
      case 3:
        return 'Từ chối';
      case 4:
        return 'Đang bổ sung';
      case 5:
        return 'Hoàn thành';
      default:
        return 'Không xác định';
    }
  }

  statusKey(status: number): string {
    switch (status) {
      case 1:
        return 'draft';
      case 2:
        return 'submitted';
      case 3:
        return 'rejected';
      case 4:
        return 'submitted';
      case 5:
        return 'locked';
      default:
        return '';
    }
  }

  canAct(item: YeuCauBoSungDto): boolean {
    return this.canApprove && item.trangThai === 1;
  }

  formatDate(value?: string): string {
    return value ? new Date(value).toLocaleString('vi-VN') : '-';
  }

  kyStatusLabel(status: number): string {
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

  private async loadRequests(kyBaoCaoId: number): Promise<void> {
    this.loading = true;
    try {
      this.requests = await this.yeuCauApi.getByKy(kyBaoCaoId);
    } catch (error: unknown) {
      this.requests = [];
      this.notificationService.show('error', this.extractError(error));
    } finally {
      this.loading = false;
    }
  }

  private flattenDonViOptions(
    items: Array<{
      id: number;
      maDonVi: string;
      tenDonVi: string;
      children: unknown[];
    }>,
    level = 0,
  ): DonViOption[] {
    const result: DonViOption[] = [];
    for (const item of items) {
      const prefix = level > 0 ? `${'--'.repeat(level)} ` : '';
      result.push({
        label: `${prefix}${item.tenDonVi} (${item.maDonVi})`,
        value: item.id,
      });

      const children = Array.isArray(item.children)
        ? (item.children as Array<{
            id: number;
            maDonVi: string;
            tenDonVi: string;
            children: unknown[];
          }>)
        : [];

      result.push(...this.flattenDonViOptions(children, level + 1));
    }

    return result;
  }

  private normalizeDateValue(value: string): string | undefined {
    const trimmed = value.trim();
    return trimmed ? trimmed : undefined;
  }

  private resetCreateForm(): void {
    this.createModel = {
      donViId: null,
      lyDo: '',
      hanBoSung: '',
    };
  }

  private extractError(error: unknown): string {
    const message = (error as { error?: { error?: { message?: string } } })
      ?.error?.error?.message;
    return message || 'Thao tác thất bại, vui lòng thử lại.';
  }
}
