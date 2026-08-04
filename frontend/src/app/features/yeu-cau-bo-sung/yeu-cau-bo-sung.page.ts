import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { DropdownModule } from 'primeng/dropdown';
import { InputTextareaModule } from 'primeng/inputtextarea';
import { TableModule } from 'primeng/table';
import { AuthService } from '../../core/auth/auth.service';
import { NotificationService } from '../../core/ui/notification.service';
import { DonViApi } from '../don-vi/don-vi.api';
import { KyBaoCaoApi, KyBaoCaoDto } from '../ky-bao-cao/ky-bao-cao.api';
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

type BadgeTone = 'neutral' | 'info' | 'success' | 'warning' | 'danger';

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
    DialogModule,
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

  // Create dialog (sub-unit tạo cho chính mình — không cần chọn đơn vị)
  showCreateDialog = false;
  createModel = { lyDo: '', hanBoSung: '' };

  showApproveDialog = false;
  approveTarget: YeuCauBoSungDto | null = null;
  approveHanBoSung = '';

  showRejectDialog = false;
  rejectTarget: YeuCauBoSungDto | null = null;
  rejectReason = '';

  constructor(
    private readonly yeuCauApi: YeuCauBoSungApi,
    private readonly kyBaoCaoApi: KyBaoCaoApi,
    private readonly donViApi: DonViApi,
    private readonly route: ActivatedRoute,
    private readonly authService: AuthService,
    private readonly notificationService: NotificationService,
  ) {
    void this.loadInitialData();
  }

  // ── Computed ──────────────────────────────────────────────────────────────

  get currentDonViId(): number {
    return this.authService.profile()?.donViId ?? 0;
  }

  /** Cấp quản lý: xem tất cả đơn vị, có filter đơn vị */
  get isManagerView(): boolean {
    return this.canApprove;
  }

  get pageTitle(): string {
    return this.isManagerView ? 'Duyệt yêu cầu bổ sung' : 'Yêu cầu bổ sung dữ liệu';
  }

  get pageSubtitle(): string {
    return this.isManagerView
      ? 'Xem xét và xử lý các yêu cầu bổ sung từ đơn vị.'
      : 'Gửi yêu cầu cho phép bổ sung dữ liệu của kỳ báo cáo đã nộp.';
  }

  get kyOptions(): Array<{ label: string; value: number }> {
    const sorted = [...this.kyBaoCaos].sort((a, b) => {
      if (a.trangThai === 2 && b.trangThai !== 2) return -1;
      if (a.trangThai !== 2 && b.trangThai === 2) return 1;
      return 0;
    });
    return sorted.map((item) => ({
      label: item.tenKy || item.kyCode,
      value: item.id,
    }));
  }

  get selectedKy(): KyBaoCaoDto | null {
    return this.kyBaoCaos.find((k) => k.id === this.selectedKyBaoCaoId) ?? null;
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
        !this.isManagerView ||
        this.selectedDonViFilterId == null ||
        item.donViId === this.selectedDonViFilterId;
      return matchTrangThai && matchDonVi;
    });
  }

  get pendingCount(): number {
    return this.requests.filter((r) => r.trangThai === 1).length;
  }

  get emptyStateTitle(): string {
    if (this.requests.length > 0) return 'Không khớp bộ lọc';
    return this.isManagerView ? 'Chưa có yêu cầu nào' : 'Bạn chưa gửi yêu cầu nào';
  }

  get emptyStateMessage(): string {
    if (this.requests.length > 0) return 'Thử thay đổi trạng thái hoặc đơn vị.';
    return this.isManagerView
      ? 'Chưa có đơn vị nào gửi yêu cầu bổ sung trong kỳ này.'
      : 'Nếu cần bổ sung dữ liệu đã nộp, nhấn nút "Gửi yêu cầu bổ sung" ở trên.';
  }

  // ── Load ─────────────────────────────────────────────────────────────────

  async loadInitialData(): Promise<void> {
    if (!this.canRead) return;

    this.loading = true;
    try {
      const tasks: [Promise<KyBaoCaoDto[]>, Promise<unknown>] = [
        this.kyBaoCaoApi.getAll(),
        this.isManagerView ? this.donViApi.getTree() : Promise.resolve([]),
      ];
      const [allKy, donViTree] = await Promise.all(tasks);

      this.kyBaoCaos = allKy;

      const queryKyIdRaw = this.route.snapshot.queryParamMap.get('kyBaoCaoId');
      const queryKyId = queryKyIdRaw ? Number(queryKyIdRaw) : NaN;
      const selectedFromQuery = Number.isInteger(queryKyId) && queryKyId > 0
        ? (allKy.find((item) => item.id === queryKyId)?.id ?? null)
        : null;
      const dangMo = allKy.find((k) => k.trangThai === 2);
      this.selectedKyBaoCaoId = selectedFromQuery ?? dangMo?.id ?? allKy[0]?.id ?? null;

      if (this.isManagerView) {
        this.donViOptions = this.flattenDonViOptions(
          donViTree as Array<{ id: number; maDonVi: string; tenDonVi: string; children: unknown[] }>,
        );
      }

      if (this.selectedKyBaoCaoId) {
        await this.loadRequests(this.selectedKyBaoCaoId);
      }
    } finally {
      this.loading = false;
    }
  }

  async onKyChange(value: number | null | undefined): Promise<void> {
    this.selectedKyBaoCaoId = value ?? null;
    this.requests = [];
    if (!this.selectedKyBaoCaoId) return;
    await this.loadRequests(this.selectedKyBaoCaoId);
  }

  // ── Create dialog ─────────────────────────────────────────────────────────

  openCreateDialog(): void {
    this.createModel = { lyDo: '', hanBoSung: '' };
    this.showCreateDialog = true;
  }

  closeCreateDialog(): void {
    this.showCreateDialog = false;
  }

  async createRequest(): Promise<void> {
    if (!this.canCreate || !this.selectedKyBaoCaoId) return;

    const lyDo = this.createModel.lyDo.trim();
    if (!lyDo) {
      this.notificationService.show('warning', 'Vui lòng nhập lý do bổ sung.');
      return;
    }

    const payload: CreateYeuCauBoSungRequest = {
      kyBaoCaoId: this.selectedKyBaoCaoId,
      donViId: this.currentDonViId,
      lyDo,
      hanBoSung: this.normalizeDateValue(this.createModel.hanBoSung),
    };

    this.creating = true;
    try {
      await this.yeuCauApi.create(payload);
      this.notificationService.show('success', 'Đã gửi yêu cầu bổ sung. Vui lòng chờ duyệt.');
      this.closeCreateDialog();
      await this.loadRequests(this.selectedKyBaoCaoId);
    } catch (error: unknown) {
      this.notificationService.show('error', this.extractError(error));
    } finally {
      this.creating = false;
    }
  }

  // ── Approve / Reject ──────────────────────────────────────────────────────

  openApproveDialog(item: YeuCauBoSungDto): void {
    if (!this.canApprove || item.trangThai !== 1) return;
    this.approveTarget = item;
    this.approveHanBoSung = item.hanBoSung ?? '';
    this.showApproveDialog = true;
  }

  closeApproveDialog(): void {
    this.showApproveDialog = false;
    this.approveTarget = null;
    this.approveHanBoSung = '';
  }

  async confirmApprove(): Promise<void> {
    if (!this.approveTarget || !this.selectedKyBaoCaoId) return;

    this.processing = true;
    try {
      await this.yeuCauApi.duyet(this.approveTarget.id, {
        hanBoSung: this.approveHanBoSung || undefined,
      });
      this.notificationService.show('success', 'Đã duyệt yêu cầu và mở lại snapshot.');
      this.closeApproveDialog();
      await this.loadRequests(this.selectedKyBaoCaoId);
    } catch (error: unknown) {
      this.notificationService.show('error', this.extractError(error));
    } finally {
      this.processing = false;
    }
  }

  openRejectDialog(item: YeuCauBoSungDto): void {
    if (!this.canApprove || item.trangThai !== 1) return;
    this.rejectTarget = item;
    this.rejectReason = '';
    this.showRejectDialog = true;
  }

  closeRejectDialog(): void {
    this.showRejectDialog = false;
    this.rejectTarget = null;
    this.rejectReason = '';
  }

  async submitReject(): Promise<void> {
    if (!this.rejectTarget || !this.selectedKyBaoCaoId) return;

    const reason = this.rejectReason.trim();
    if (!reason) {
      this.notificationService.show('warning', 'Cần nhập lý do từ chối.');
      return;
    }

    this.processing = true;
    try {
      await this.yeuCauApi.tuChoi(this.rejectTarget.id, { tuChoiLyDo: reason });
      this.notificationService.show('success', 'Đã từ chối yêu cầu bổ sung.');
      this.closeRejectDialog();
      await this.loadRequests(this.selectedKyBaoCaoId);
    } catch (error: unknown) {
      this.notificationService.show('error', this.extractError(error));
    } finally {
      this.processing = false;
    }
  }

  canAct(item: YeuCauBoSungDto): boolean {
    return this.canApprove && item.trangThai === 1;
  }

  // ── Helpers ───────────────────────────────────────────────────────────────

  statusLabel(status: number): string {
    const map: Record<number, string> = {
      1: 'Chờ duyệt', 2: 'Đã duyệt', 3: 'Từ chối', 4: 'Đang bổ sung', 5: 'Hoàn thành',
    };
    return map[status] ?? 'Không xác định';
  }

  statusTone(status: number): BadgeTone {
    const map: Record<number, BadgeTone> = {
      1: 'warning', 2: 'info', 3: 'danger', 4: 'warning', 5: 'success',
    };
    return map[status] ?? 'neutral';
  }

  relevantTimestamp(item: YeuCauBoSungDto): { label: string; value: string } | null {
    if (item.completedAt) return { label: 'Hoàn thành', value: item.completedAt };
    if (item.approvedAt) return { label: item.trangThai === 3 ? 'Từ chối' : 'Duyệt', value: item.approvedAt };
    return { label: 'Gửi lúc', value: item.requestedAt };
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
    items: Array<{ id: number; maDonVi: string; tenDonVi: string; children: unknown[] }>,
    level = 0,
  ): DonViOption[] {
    const result: DonViOption[] = [];
    for (const item of items) {
      const prefix = level > 0 ? `${'–'.repeat(level)} ` : '';
      result.push({ label: `${prefix}${item.tenDonVi}`, value: item.id });
      const children = Array.isArray(item.children)
        ? (item.children as Array<{ id: number; maDonVi: string; tenDonVi: string; children: unknown[] }>)
        : [];
      result.push(...this.flattenDonViOptions(children, level + 1));
    }
    return result;
  }

  private normalizeDateValue(value: string): string | undefined {
    const trimmed = value.trim();
    return trimmed ? trimmed : undefined;
  }

  private extractError(error: unknown): string {
    const r = (error as { error?: { error?: { message?: string; Message?: string }; Error?: { message?: string; Message?: string } } })?.error;
    return (
      r?.error?.message ??
      r?.error?.Message ??
      r?.Error?.message ??
      r?.Error?.Message ??
      'Thao tác thất bại, vui lòng thử lại.'
    );
  }
}
