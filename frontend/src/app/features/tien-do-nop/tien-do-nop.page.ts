import { CommonModule } from '@angular/common';
import { Component, ElementRef, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { PrimeNGConfig } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CalendarModule } from 'primeng/calendar';
import { DialogModule } from 'primeng/dialog';
import { DropdownModule } from 'primeng/dropdown';
import { InputTextModule } from 'primeng/inputtext';
import { InputTextareaModule } from 'primeng/inputtextarea';
import { TableModule } from 'primeng/table';
import { TooltipModule } from 'primeng/tooltip';
import { NotificationService } from '../../core/ui/notification.service';
import { AuthService } from '../../core/auth/auth.service';
import { EmptyStateComponent } from '../../shared/ui/empty-state.component';
import { LoadingOverlayComponent } from '../../shared/ui/loading-overlay.component';
import { SectionCardComponent } from '../../shared/ui/section-card.component';
import { StatusBadgeComponent } from '../../shared/ui/status-badge.component';
import {
  KyBaoCaoApi,
  KyBaoCaoDto,
  KyBaoCaoTienDoDonViDto,
  KyBaoCaoTienDoDto,
} from '../ky-bao-cao/ky-bao-cao.api';
import {
  YeuCauBoSungApi,
  YeuCauBoSungDto,
} from '../yeu-cau-bo-sung/yeu-cau-bo-sung.api';
import { SnapshotApi } from '../snapshot/snapshot.api';

type BadgeTone = 'neutral' | 'info' | 'success' | 'warning' | 'danger';

const CALENDAR_LOCALE_VI = {
  firstDayOfWeek: 1,
  dayNames: ['Chủ nhật', 'Thứ Hai', 'Thứ Ba', 'Thứ Tư', 'Thứ Năm', 'Thứ Sáu', 'Thứ Bảy'],
  dayNamesShort: ['CN', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7'],
  dayNamesMin: ['CN', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7'],
  monthNames: ['Tháng 1', 'Tháng 2', 'Tháng 3', 'Tháng 4', 'Tháng 5', 'Tháng 6', 'Tháng 7', 'Tháng 8', 'Tháng 9', 'Tháng 10', 'Tháng 11', 'Tháng 12'],
  monthNamesShort: ['T1', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'T8', 'T9', 'T10', 'T11', 'T12'],
  today: 'Hôm nay',
  clear: 'Xóa',
  weekHeader: 'Tuần',
};

@Component({
  selector: 'app-tien-do-nop-page',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    SectionCardComponent,
    StatusBadgeComponent,
    EmptyStateComponent,
    LoadingOverlayComponent,
    ButtonModule,
    DialogModule,
    DropdownModule,
    InputTextModule,
    CalendarModule,
    InputTextareaModule,
    TableModule,
    TooltipModule,
  ],
  templateUrl: './tien-do-nop.page.html',
  styleUrls: ['./tien-do-nop.page.scss'],
})
export class TienDoNopPage implements OnInit, OnDestroy {
  @ViewChild('previewFrame') previewFrame?: ElementRef<HTMLIFrameElement>;

  loading = false;
  loadingTienDo = false;
  allKy: KyBaoCaoDto[] = [];
  selectedKyId: number | null = null;
  tienDo: KyBaoCaoTienDoDto | null = null;
  showYcbsDialog = false;
  ycbsTargetDonViId: number | null = null;
  ycbsLyDo = '';
  ycbsHanBoSung: Date | null = null;
  ycbsLoading = false;
  ycbsError = '';
  filterText = '';
  filterStatus: number | null = null;
  sortMode = 'status';
  tableFirst = 0;
  showPreviewDialog = false;
  previewLoadingId: number | null = null;
  previewPdfUrl: SafeResourceUrl | null = null;
  previewInlineUrl: string | null = null;
  previewRow: KyBaoCaoTienDoDonViDto | null = null;
  requests: YeuCauBoSungDto[] = [];
  private requestByUnitId = new Map<number, YeuCauBoSungDto>();
  showApproveDialog = false;
  approveTarget: YeuCauBoSungDto | null = null;
  approveHanBoSung = '';
  showRejectDialog = false;
  rejectTarget: YeuCauBoSungDto | null = null;
  rejectReason = '';
  requestProcessing = false;
  private previewObjectUrl: string | null = null;
  readonly ycbsMinDate = new Date();

  readonly statusOptions = [
    { label: 'Tất cả trạng thái', value: null },
    { label: 'Chưa nộp', value: 1 },
    { label: 'Đang nhập', value: 2 },
    { label: 'Đã xác nhận', value: 3 },
    { label: 'Đang bổ sung', value: 4 },
    { label: 'Đã nộp', value: 5 },
  ];

  readonly sortOptions = [
    { label: 'Ưu tiên cần xử lý', value: 'status' },
    { label: 'Tên đơn vị A–Z', value: 'nameAsc' },
    { label: 'Tên đơn vị Z–A', value: 'nameDesc' },
    { label: 'Nộp mới nhất', value: 'submittedDesc' },
    { label: 'Nộp cũ nhất', value: 'submittedAsc' },
  ];

  readonly donViStatusMap: Record<number, { label: string; tone: BadgeTone }> =
    {
      1: { label: 'Chưa nộp', tone: 'neutral' },
      2: { label: 'Đang nhập', tone: 'warning' },
      3: { label: 'Đã xác nhận', tone: 'success' },
      4: { label: 'Đang bổ sung', tone: 'warning' },
      5: { label: 'Đã nộp', tone: 'success' },
    };

  constructor(
    private readonly kyBaoCaoApi: KyBaoCaoApi,
    private readonly yeuCauApi: YeuCauBoSungApi,
    private readonly snapshotApi: SnapshotApi,
    private readonly notificationService: NotificationService,
    private readonly authService: AuthService,
    private readonly route: ActivatedRoute,
    private readonly primeNgConfig: PrimeNGConfig,
    private readonly sanitizer: DomSanitizer,
  ) {
    this.primeNgConfig.setTranslation(CALENDAR_LOCALE_VI);
  }

  get ycbsTargetTenDonVi(): string {
    if (!this.ycbsTargetDonViId || !this.tienDo) return '';
    return (
      this.tienDo.donVis.find((d) => d.donViId === this.ycbsTargetDonViId)
        ?.tenDonVi ?? ''
    );
  }

  get selectedKyDisplayName(): string {
    const ky = this.allKy.find((k) => k.id === this.selectedKyId);
    return ky ? ky.tenKy || ky.kyCode : '';
  }

  ngOnInit(): void {
    void this.load();
  }

  ngOnDestroy(): void {
    this.revokePreviewObjectUrl();
  }

  get kyOptions(): { label: string; value: number; code: string }[] {
    const sorted = [...this.allKy].sort((a, b) => {
      const startDifference = this.periodStartTime(b) - this.periodStartTime(a);
      if (startDifference !== 0) return startDifference;

      const endDifference = this.periodEndTime(b) - this.periodEndTime(a);
      if (endDifference !== 0) return endDifference;

      return b.id - a.id;
    });
    return sorted.map((k) => ({
      label: k.tenKy || k.kyCode,
      value: k.id,
      code: k.kyCode,
    }));
  }

  get selectedKy(): KyBaoCaoDto | null {
    return this.allKy.find((k) => k.id === this.selectedKyId) ?? null;
  }

  get canManageRequests(): boolean {
    return this.authService.hasPermission('yeu_cau_bo_sung:approve');
  }

  get tienDoRows(): KyBaoCaoTienDoDonViDto[] {
    return this.tienDo?.danhSachDonVi ?? this.tienDo?.donVis ?? [];
  }

  get filteredTienDoRows(): KyBaoCaoTienDoDonViDto[] {
    const query = this.filterText.trim().toLocaleLowerCase('vi');
    const rows = this.tienDoRows.filter((row) => {
      const matchesStatus = this.filterStatus == null || row.trangThai === this.filterStatus;
      const matchesText =
        !query ||
        row.tenDonVi.toLocaleLowerCase('vi').includes(query) ||
        (row.maDonVi ?? '').toLocaleLowerCase('vi').includes(query);
      return matchesStatus && matchesText;
    });

    return rows.sort((a, b) => {
      if (this.sortMode === 'nameAsc') return this.compareUnitName(a, b);
      if (this.sortMode === 'nameDesc') return this.compareUnitName(b, a);
      if (this.sortMode === 'submittedDesc') {
        return this.submittedTime(b) - this.submittedTime(a) || this.compareUnitName(a, b);
      }
      if (this.sortMode === 'submittedAsc') {
        const aTime = this.submittedTime(a);
        const bTime = this.submittedTime(b);
        if (!aTime && bTime) return 1;
        if (aTime && !bTime) return -1;
        return aTime - bTime || this.compareUnitName(a, b);
      }

      const aRequest = this.requestForUnit(a.donViId);
      const bRequest = this.requestForUnit(b.donViId);
      const aPriority = aRequest?.trangThai === 1 ? -1 : this.progressPriority(a.trangThai);
      const bPriority = bRequest?.trangThai === 1 ? -1 : this.progressPriority(b.trangThai);
      return aPriority - bPriority || this.compareUnitName(a, b);
    });
  }

  requestForUnit(donViId: number): YeuCauBoSungDto | null {
    return this.requestByUnitId.get(donViId) ?? null;
  }

  requestStatusLabel(status: number): string {
    const labels: Record<number, string> = {
      1: 'Chờ duyệt',
      2: 'Đã duyệt',
      3: 'Từ chối',
      4: 'Đang bổ sung',
      5: 'Hoàn thành',
    };
    return labels[status] ?? 'Không xác định';
  }

  requestStatusTone(status: number): BadgeTone {
    const tones: Record<number, BadgeTone> = {
      1: 'warning',
      2: 'info',
      3: 'danger',
      4: 'warning',
      5: 'success',
    };
    return tones[status] ?? 'neutral';
  }

  hasActiveRequest(donViId: number): boolean {
    const status = this.requestForUnit(donViId)?.trangThai;
    return status === 1 || status === 2 || status === 4;
  }

  openApproveDialog(request: YeuCauBoSungDto): void {
    if (!this.canManageRequests || request.trangThai !== 1) return;
    this.approveTarget = request;
    this.approveHanBoSung = request.hanBoSung ?? '';
    this.showApproveDialog = true;
  }

  closeApproveDialog(): void {
    this.showApproveDialog = false;
    this.approveTarget = null;
    this.approveHanBoSung = '';
  }

  async confirmApprove(): Promise<void> {
    if (!this.approveTarget) return;
    this.requestProcessing = true;
    try {
      await this.yeuCauApi.duyet(this.approveTarget.id, {
        hanBoSung: this.approveHanBoSung || undefined,
      });
      this.notificationService.show('success', 'Đã duyệt yêu cầu và mở lại báo cáo.');
      this.closeApproveDialog();
      await this.loadTienDo();
    } catch (error: unknown) {
      this.notificationService.show('error', this.extractError(error));
    } finally {
      this.requestProcessing = false;
    }
  }

  openRejectDialog(request: YeuCauBoSungDto): void {
    if (!this.canManageRequests || request.trangThai !== 1) return;
    this.rejectTarget = request;
    this.rejectReason = '';
    this.showRejectDialog = true;
  }

  closeRejectDialog(): void {
    this.showRejectDialog = false;
    this.rejectTarget = null;
    this.rejectReason = '';
  }

  async confirmReject(): Promise<void> {
    if (!this.rejectTarget) return;
    const reason = this.rejectReason.trim();
    if (!reason) {
      this.notificationService.show('warning', 'Vui lòng nhập lý do từ chối.');
      return;
    }

    this.requestProcessing = true;
    try {
      await this.yeuCauApi.tuChoi(this.rejectTarget.id, { tuChoiLyDo: reason });
      this.notificationService.show('success', 'Đã từ chối yêu cầu bổ sung.');
      this.closeRejectDialog();
      await this.loadTienDo();
    } catch (error: unknown) {
      this.notificationService.show('error', this.extractError(error));
    } finally {
      this.requestProcessing = false;
    }
  }

  get hasActiveFilters(): boolean {
    return this.filterText.trim().length > 0 || this.filterStatus !== null;
  }

  onTableCriteriaChange(): void {
    this.tableFirst = 0;
  }

  clearTableFilters(): void {
    this.filterText = '';
    this.filterStatus = null;
    this.sortMode = 'status';
    this.tableFirst = 0;
  }

  async openSnapshotPreview(row: KyBaoCaoTienDoDonViDto): Promise<void> {
    if (!row.snapshotId) return;

    this.previewLoadingId = row.snapshotId;
    this.previewRow = row;
    this.previewPdfUrl = null;
    this.previewInlineUrl = null;
    this.revokePreviewObjectUrl();
    this.showPreviewDialog = true;
    try {
      const result = await this.snapshotApi.getPdf(row.snapshotId);
      this.previewInlineUrl = result.previewUrl || result.downloadUrl;
      const response = await fetch(this.previewInlineUrl);
      if (!response.ok) throw new Error('PREVIEW_FETCH_FAILED');

      this.previewObjectUrl = URL.createObjectURL(await response.blob());
      this.previewPdfUrl = this.sanitizer.bypassSecurityTrustResourceUrl(this.previewObjectUrl);
    } catch {
      this.showPreviewDialog = false;
      this.notificationService.show('error', 'Không thể tải báo cáo để xem trước.');
    } finally {
      this.previewLoadingId = null;
    }
  }

  closePreview(): void {
    this.showPreviewDialog = false;
    this.previewRow = null;
    this.previewPdfUrl = null;
    this.previewInlineUrl = null;
    this.revokePreviewObjectUrl();
  }

  openPreviewInNewTab(): void {
    if (this.previewInlineUrl) window.open(this.previewInlineUrl, '_blank');
  }

  printPreview(): void {
    const frameWindow = this.previewFrame?.nativeElement.contentWindow;
    if (frameWindow) {
      try {
        frameWindow.focus();
        frameWindow.print();
        return;
      } catch {
        // Trình duyệt có thể chặn lệnh in iframe; mở tab mới để người dùng in.
      }
    }
    this.openPreviewInNewTab();
  }

  private revokePreviewObjectUrl(): void {
    if (this.previewObjectUrl) {
      URL.revokeObjectURL(this.previewObjectUrl);
      this.previewObjectUrl = null;
    }
  }

  private compareUnitName(a: KyBaoCaoTienDoDonViDto, b: KyBaoCaoTienDoDonViDto): number {
    return a.tenDonVi.localeCompare(b.tenDonVi, 'vi', { sensitivity: 'base' });
  }

  private submittedTime(row: KyBaoCaoTienDoDonViDto): number {
    return row.submittedAt ? new Date(row.submittedAt).getTime() : 0;
  }

  private progressPriority(status: number): number {
    const priority: Record<number, number> = { 4: 0, 2: 1, 1: 2, 5: 3, 3: 4 };
    return priority[status] ?? 9;
  }

  private periodStartTime(period: KyBaoCaoDto): number {
    if (period.ngayBatDau) return Date.parse(period.ngayBatDau) || 0;

    const month = period.thang
      ? period.thang - 1
      : period.quy
        ? (period.quy - 1) * 3
        : 0;
    return Date.UTC(period.nam, month, 1);
  }

  private periodEndTime(period: KyBaoCaoDto): number {
    if (period.ngayKetThuc) return Date.parse(period.ngayKetThuc) || 0;
    return this.periodStartTime(period);
  }

  async load(): Promise<void> {
    this.loading = true;
    try {
      this.allKy = await this.kyBaoCaoApi.getAll();
      const queryKyIdRaw = this.route.snapshot.queryParamMap.get('kyId');
      const queryKyId = queryKyIdRaw ? Number(queryKyIdRaw) : NaN;
      const selectedFromQuery = Number.isInteger(queryKyId)
        ? (this.allKy.find((item) => item.id === queryKyId)?.id ?? null)
        : null;
      const openKy = this.allKy.find((item) => item.trangThai === 2);
      const targetKy =
        (selectedFromQuery
          ? this.allKy.find((item) => item.id === selectedFromQuery)
          : null) ??
        openKy ??
        this.allKy[0] ??
        null;
      this.selectedKyId = targetKy?.id ?? null;

      if (this.selectedKyId) {
        await this.loadTienDo();
      } else {
        this.tienDo = null;
      }
    } catch (error: unknown) {
      this.tienDo = null;
      this.notificationService.show('error', this.extractError(error));
    } finally {
      this.loading = false;
    }
  }

  async loadTienDo(): Promise<void> {
    if (!this.selectedKyId) {
      this.tienDo = null;
      this.requests = [];
      this.requestByUnitId.clear();
      return;
    }

    this.loadingTienDo = true;
    try {
      const requestPromise = this.canManageRequests
        ? this.yeuCauApi.getByKy(this.selectedKyId).catch((error: unknown) => {
            this.notificationService.show('error', this.extractError(error));
            return [] as YeuCauBoSungDto[];
          })
        : Promise.resolve([] as YeuCauBoSungDto[]);
      const [progress, requests] = await Promise.all([
        this.kyBaoCaoApi.getTienDo(this.selectedKyId),
        requestPromise,
      ]);
      this.tienDo = progress;
      this.requests = requests;
      this.indexRequestsByUnit(requests);
    } catch (error: unknown) {
      this.tienDo = null;
      this.requests = [];
      this.requestByUnitId.clear();
      this.notificationService.show('error', this.extractError(error));
    } finally {
      this.loadingTienDo = false;
    }
  }

  openYcbsDialog(donViId: number): void {
    this.ycbsTargetDonViId = donViId;
    this.ycbsLyDo = '';
    this.ycbsHanBoSung = null;
    this.ycbsError = '';
    this.showYcbsDialog = true;
  }

  closeYcbsDialog(): void {
    this.showYcbsDialog = false;
  }

  async confirmYcbs(): Promise<void> {
    if (!this.ycbsLyDo.trim()) {
      this.ycbsError = 'Vui lòng nhập lý do yêu cầu.';
      return;
    }

    if (!this.selectedKyId || !this.ycbsTargetDonViId) {
      return;
    }

    this.ycbsLoading = true;
    this.ycbsError = '';
    try {
      await this.yeuCauApi.create({
        kyBaoCaoId: this.selectedKyId,
        donViId: this.ycbsTargetDonViId,
        lyDo: this.ycbsLyDo.trim(),
        // Không dùng toISOString() — lệch ngày do timezone
        hanBoSung: this.ycbsHanBoSung
          ? `${this.ycbsHanBoSung.getFullYear()}-${String(this.ycbsHanBoSung.getMonth() + 1).padStart(2, '0')}-${String(this.ycbsHanBoSung.getDate()).padStart(2, '0')}`
          : undefined,
      });
      this.showYcbsDialog = false;
      this.notificationService.show(
        'success',
        'Đã gửi yêu cầu bổ sung thành công.',
      );
      await this.loadTienDo();
    } catch (error: unknown) {
      this.ycbsError = this.extractError(error);
    } finally {
      this.ycbsLoading = false;
    }
  }

  get progressPercent(): number {
    if (!this.tienDo?.tongDonVi) return 0;
    return Math.round((this.tienDo.soDonViDaNop / this.tienDo.tongDonVi) * 100);
  }

  donViStatusLabel(status: number): string {
    return this.donViStatusMap[status]?.label ?? '—';
  }

  donViStatusTone(status: number): BadgeTone {
    return this.donViStatusMap[status]?.tone ?? 'neutral';
  }

  private extractError(error: unknown): string {
    const r = (error as { error?: { error?: { message?: string; Message?: string }; Error?: { message?: string; Message?: string } } })?.error;
    return (
      r?.error?.message ??
      r?.error?.Message ??
      r?.Error?.message ??
      r?.Error?.Message ??
      'Không thể tải dữ liệu hoặc gửi yêu cầu. Vui lòng thử lại.'
    );
  }

  private indexRequestsByUnit(requests: YeuCauBoSungDto[]): void {
    const activePriority: Record<number, number> = { 1: 0, 4: 1, 2: 2, 3: 3, 5: 4 };
    const sorted = [...requests].sort((a, b) => {
      return (
        (activePriority[a.trangThai] ?? 9) - (activePriority[b.trangThai] ?? 9) ||
        new Date(b.requestedAt).getTime() - new Date(a.requestedAt).getTime()
      );
    });

    this.requestByUnitId = new Map<number, YeuCauBoSungDto>();
    for (const request of sorted) {
      if (!this.requestByUnitId.has(request.donViId)) {
        this.requestByUnitId.set(request.donViId, request);
      }
    }
  }
}
