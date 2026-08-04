import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { PrimeNGConfig } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CalendarModule } from 'primeng/calendar';
import { DialogModule } from 'primeng/dialog';
import { DropdownModule } from 'primeng/dropdown';
import { InputTextModule } from 'primeng/inputtext';
import { InputTextareaModule } from 'primeng/inputtextarea';
import { TableModule } from 'primeng/table';
import { AuthService } from '../../core/auth/auth.service';
import { NotificationService } from '../../core/ui/notification.service';
import { EmptyStateComponent } from '../../shared/ui/empty-state.component';
import { LoadingOverlayComponent } from '../../shared/ui/loading-overlay.component';
import { SectionCardComponent } from '../../shared/ui/section-card.component';
import { StatusBadgeComponent } from '../../shared/ui/status-badge.component';
import { KyBaoCaoApi, KyBaoCaoDto } from '../ky-bao-cao/ky-bao-cao.api';
import {
  SnapshotApi,
  SnapshotDto,
  SubmitSnapshotContextDto,
} from '../snapshot/snapshot.api';
import {
  YeuCauBoSungApi,
  YeuCauBoSungDto,
} from '../yeu-cau-bo-sung/yeu-cau-bo-sung.api';

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
  selector: 'app-nop-bao-cao-page',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
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
  ],
  templateUrl: './nop-bao-cao.page.html',
  styleUrls: ['./nop-bao-cao.page.scss'],
})
export class NopBaoCaoPage implements OnInit {
  loading = false;
  allKy: KyBaoCaoDto[] = [];
  selectedKyId: number | null = null;
  ownSnapshots: SnapshotDto[] = [];
  pendingYeuCau: YeuCauBoSungDto[] = [];
  allYeuCau: YeuCauBoSungDto[] = [];
  showNopDialog = false;
  ghiChuNop = '';
  nopLoading = false;
  nopError = '';
  nopCountdown = 5;
  private nopTimer: ReturnType<typeof setInterval> | null = null;

  showYeuCauDialog = false;
  yeuCauLyDo = '';
  yeuCauHan: Date | null = null;
  yeuCauLoading = false;
  yeuCauError = '';
  readonly yeuCauMinDate = new Date();

  readonly yeuCauStatusMap: Record<number, { label: string; tone: BadgeTone }> = {
    1: { label: 'Chờ duyệt', tone: 'warning' },
    2: { label: 'Đã duyệt', tone: 'info' },
    3: { label: 'Từ chối', tone: 'danger' },
    4: { label: 'Đang bổ sung', tone: 'warning' },
    5: { label: 'Hoàn thành', tone: 'success' },
  };

  moduleList: string[] = [];
  moduleStatus: Record<string, number> = {};
  /** TONG_HOP: phân rã Tự nhập / Từ đơn vị con cho từng module. */
  moduleStatusDetail: Record<string, { own: number; child: number }> = {};
  donViTrangThai = 1;
  submitContext: SubmitSnapshotContextDto | null = null;

  readonly moduleLabels: Record<string, string> = {
    NHAN_LUC_CNTT: 'Nhân lực CNTT',
    NANG_LUC_SO: 'Năng lực số',
    DAO_TAO_BOI_DUONG: 'Đào tạo bồi dưỡng',
    DAO_TAO_HOC_VIEN: 'Đào tạo học viện',
    HE_THONG_THONG_TIN: 'Hệ thống thông tin',
    HTTT_TIEU_CHUAN: 'HTTT tiêu chuẩn',
    DU_AN_CNTT: 'Dự án CNTT',
    THIET_BI_CNTT: 'Thiết bị CNTT',
    HA_TANG_MANG: 'Hạ tầng mạng',
    GIAM_SAT_NOC: 'Giám sát NOC',
    CAMERA_QUAN_LY: 'Camera quản lý',
    CAMERA_THUC_TRANG: 'Camera thực trạng',
    GIAM_SAT_SOC: 'Giám sát SOC',
    ATTT_HTTT_VAN_HANH: 'ATTT hệ thống vận hành',
    ATTT_HTTT_DAU_TU: 'ATTT hệ thống đầu tư',
    ATTT_GIAI_PHAP: 'Giải pháp ATTT',
    VAN_BAN_QPPL: 'Văn bản QPPL',
  };

  readonly donViTrangThaiMap: Record<
    number,
    { label: string; tone: BadgeTone; icon: string; desc: string }
  > = {
    1: {
      label: 'Chưa nhập',
      tone: 'neutral',
      icon: 'pi-circle',
      desc: 'Đơn vị chưa bắt đầu nhập liệu',
    },
    2: {
      label: 'Đang nhập',
      tone: 'warning',
      icon: 'pi-spinner',
      desc: 'Đơn vị đang nhập dữ liệu',
    },
    3: {
      label: 'Đã xác nhận',
      tone: 'info',
      icon: 'pi-check-circle',
      desc: 'Dữ liệu đã được xác nhận nội bộ',
    },
    4: {
      label: 'Đang bổ sung',
      tone: 'warning',
      icon: 'pi-refresh',
      desc: 'Được yêu cầu bổ sung — cần nhập thêm',
    },
    5: {
      label: 'Đã nộp',
      tone: 'success',
      icon: 'pi-send',
      desc: 'Báo cáo đã được nộp',
    },
  };

  readonly snapshotStatusMap: Record<
    number,
    { label: string; tone: BadgeTone }
  > = {
    1: { label: 'Bản nháp', tone: 'neutral' },
    2: { label: 'Đã nộp', tone: 'success' },
    3: { label: 'Đã khóa (hiện hành)', tone: 'warning' },
    4: { label: 'Đã thay thế — do mở lại bổ sung', tone: 'neutral' },
  };

  constructor(
    private readonly kyBaoCaoApi: KyBaoCaoApi,
    private readonly snapshotApi: SnapshotApi,
    private readonly yeuCauApi: YeuCauBoSungApi,
    private readonly authService: AuthService,
    private readonly notificationService: NotificationService,
    private readonly primeNgConfig: PrimeNGConfig,
  ) {
    this.primeNgConfig.setTranslation(CALENDAR_LOCALE_VI);
  }

  ngOnInit(): void {
    void this.load();
  }

  get currentKy(): KyBaoCaoDto | null {
    return this.allKy.find((k) => k.id === this.selectedKyId) ?? null;
  }

  get dangMoKy(): KyBaoCaoDto[] {
    return this.allKy.filter((k) => k.trangThai === 2);
  }

  get kyOptions(): { label: string; value: number }[] {
    return this.dangMoKy.map((k) => ({
      label: k.tenKy || k.kyCode,
      value: k.id,
    }));
  }

  get currentDonViId(): number {
    return this.authService.profile()?.donViId ?? 0;
  }

  get latestSnapshot(): SnapshotDto | null {
    return this.ownSnapshots[0] ?? null;
  }

  get canSubmit(): boolean {
    if (
      !this.currentKy ||
      this.currentKy.trangThai !== 2 ||
      !this.currentDonViId
    )
      return false;
    if (this.latestSnapshot?.trangThai === 3) return false;
    return true;
  }

  get isLocked(): boolean {
    return this.latestSnapshot?.trangThai === 3;
  }

  get canRequestYeuCau(): boolean {
    return (
      this.isLocked &&
      !this.hasPendingYeuCau &&
      this.currentKy?.trangThai === 2
    );
  }

  yeuCauStatusLabel(status: number): string {
    return this.yeuCauStatusMap[status]?.label ?? '—';
  }

  yeuCauStatusTone(status: number): BadgeTone {
    return this.yeuCauStatusMap[status]?.tone ?? 'neutral';
  }

  get activityLog(): Array<{
    sortKey: number;
    timestamp: string | undefined;
    kind: 'nop' | 'yeucau';
    icon: string;
    title: string;
    detail: string;
    statusLabel: string;
    statusTone: BadgeTone;
  }> {
    const nopRows = this.ownSnapshots.map((snap) => ({
      sortKey: snap.submittedAt ? new Date(snap.submittedAt).getTime() : 0,
      timestamp: snap.submittedAt,
      kind: 'nop' as const,
      icon: 'pi-send',
      title: `Nộp báo cáo — phiên bản #${snap.phienBan}`,
      detail: snap.ghiChu || '—',
      statusLabel: this.snapshotStatusLabel(snap.trangThai),
      statusTone: this.snapshotStatusTone(snap.trangThai),
    }));

    const yeuCauRows = this.allYeuCau.map((yc) => ({
      sortKey: yc.requestedAt ? new Date(yc.requestedAt).getTime() : 0,
      timestamp: yc.requestedAt,
      kind: 'yeucau' as const,
      icon: 'pi-exclamation-circle',
      title: 'Yêu cầu bổ sung',
      detail: yc.lyDo,
      statusLabel: this.yeuCauStatusLabel(yc.trangThai),
      statusTone: this.yeuCauStatusTone(yc.trangThai),
    }));

    return [...nopRows, ...yeuCauRows].sort((a, b) => b.sortKey - a.sortKey);
  }

  get hasPendingYeuCau(): boolean {
    return this.pendingYeuCau.length > 0;
  }

  get hasEmptyModules(): boolean {
    return this.moduleList.some((code) => (this.moduleStatus[code] ?? 0) === 0);
  }

  get isTongHopMode(): boolean {
    return this.submitContext?.isTongHop === true;
  }

  get hasUnconfirmedChildren(): boolean {
    return this.submitContext?.hasUnconfirmedChildren === true;
  }

  get unconfirmedChildrenCount(): number {
    if (!this.submitContext) return 0;
    return (
      this.submitContext.totalChildren - this.submitContext.confirmedChildren
    );
  }

  get emptyModuleCount(): number {
    return this.moduleList.filter(
      (code) => (this.moduleStatus[code] ?? 0) === 0,
    ).length;
  }

  moduleRecordCount(code: string): number {
    return this.moduleStatus[code] ?? 0;
  }

  async load(): Promise<void> {
    this.loading = true;
    try {
      this.allKy = await this.kyBaoCaoApi.getAll();
      const dangMo = this.allKy.filter((k) => k.trangThai === 2);
      // keep selected if still valid, else pick first DangMo
      if (!dangMo.find((k) => k.id === this.selectedKyId)) {
        this.selectedKyId = dangMo[0]?.id ?? null;
      }
      await this.loadKyData();
    } catch (error: unknown) {
      this.notificationService.show('error', this.extractError(error));
    } finally {
      this.loading = false;
    }
  }

  async onKyChange(): Promise<void> {
    await this.loadKyData();
  }

  private async loadKyData(): Promise<void> {
    this.ownSnapshots = [];
    this.pendingYeuCau = [];
    this.allYeuCau = [];
    this.moduleList = [];
    this.moduleStatus = {};
    this.moduleStatusDetail = {};
    this.donViTrangThai = 1;
    this.submitContext = null;

    const ky = this.currentKy;
    if (!ky) return;

    const [snapshots, yeuCau, donViTrangThai, moduleStatusList, submitContext] =
      await Promise.all([
        this.snapshotApi.getByKy(ky.id),
        this.yeuCauApi.getByKy(ky.id),
        this.kyBaoCaoApi.getDonViTrangThai(ky.id).catch(() => 1),
        this.snapshotApi.getModuleStatus(ky.id).catch(() => []),
        this.snapshotApi.getSubmitContext(ky.id).catch(() => null),
      ]);

    this.ownSnapshots = snapshots
      .filter((item) => item.donViId === this.currentDonViId)
      .sort((a, b) => (b.phienBan ?? 0) - (a.phienBan ?? 0));

    this.allYeuCau = yeuCau
      .filter((item) => item.donViId === this.currentDonViId)
      .sort((a, b) => b.id - a.id);
    this.pendingYeuCau = this.allYeuCau.filter(
      (item) => item.trangThai <= 2 || item.trangThai === 4,
    );

    this.donViTrangThai = donViTrangThai;
    this.submitContext = submitContext;
    this.moduleList = ky.moduleList ?? [];
    this.moduleStatus = Object.fromEntries(
      moduleStatusList.map((m) => [m.moduleCode, m.recordCount]),
    );
    this.moduleStatusDetail = Object.fromEntries(
      moduleStatusList.map((m) => [
        m.moduleCode,
        {
          own: m.ownRecordCount ?? m.recordCount,
          child: m.childRecordCount ?? 0,
        },
      ]),
    );
  }

  moduleBreakdownLabel(code: string): string {
    const detail = this.moduleStatusDetail[code];
    if (!detail) return '';
    return `Gộp từ đơn vị con: ${detail.child}`;
  }

  openNopDialog(): void {
    this.ghiChuNop = '';
    this.nopError = '';
    if (this.hasEmptyModules) {
      this.nopError = `Còn ${this.emptyModuleCount} module chưa có dữ liệu. Bạn vẫn có thể nộp nhưng nên kiểm tra lại.`;
    }
    if (this.hasUnconfirmedChildren) {
      this.nopError =
        `Con ${this.unconfirmedChildrenCount} đơn vị con chưa xác nhận. ` +
        'Nếu tiếp tục, hệ thống sẽ chốt báo cáo tổng hợp tại thời điểm hiện tại.';
    }
    this.nopCountdown = 5;
    this.showNopDialog = true;
    this.nopTimer = setInterval(() => {
      this.nopCountdown--;
      if (this.nopCountdown <= 0) {
        clearInterval(this.nopTimer!);
        this.nopTimer = null;
      }
    }, 1000);
  }

  closeNopDialog(): void {
    this.showNopDialog = false;
    if (this.nopTimer) {
      clearInterval(this.nopTimer);
      this.nopTimer = null;
    }
  }

  async confirmNop(): Promise<void> {
    const ky = this.currentKy;
    if (!ky) return;

    this.nopLoading = true;
    this.nopError = '';

    try {
      await this.snapshotApi.submitCurrent({
        kyBaoCaoId: ky.id,
        donViId: this.currentDonViId,
        ghiChu: this.ghiChuNop.trim() || undefined,
        forceSubmitWhenChildrenUnconfirmed: this.hasUnconfirmedChildren,
      });

      this.showNopDialog = false;
      this.notificationService.show(
        'success',
        `Đã nộp báo cáo "${ky.tenKy || ky.kyCode}" thành công.`,
      );
      await this.load();
    } catch (error: unknown) {
      this.nopError = this.extractError(error);
    } finally {
      this.nopLoading = false;
    }
  }

  openYeuCauDialog(): void {
    this.yeuCauLyDo = '';
    this.yeuCauHan = null;
    this.yeuCauError = '';
    this.showYeuCauDialog = true;
  }

  private formatDateOnly(value: Date): string {
    const y = value.getFullYear();
    const m = String(value.getMonth() + 1).padStart(2, '0');
    const d = String(value.getDate()).padStart(2, '0');
    return `${y}-${m}-${d}`;
  }

  closeYeuCauDialog(): void {
    this.showYeuCauDialog = false;
  }

  async submitYeuCau(): Promise<void> {
    const ky = this.currentKy;
    if (!ky || !this.yeuCauLyDo.trim()) return;

    this.yeuCauLoading = true;
    this.yeuCauError = '';

    try {
      await this.yeuCauApi.create({
        kyBaoCaoId: ky.id,
        donViId: this.currentDonViId,
        lyDo: this.yeuCauLyDo.trim(),
        hanBoSung: this.yeuCauHan
          ? this.formatDateOnly(this.yeuCauHan)
          : undefined,
      });

      this.showYeuCauDialog = false;
      this.notificationService.show(
        'success',
        'Đã gửi yêu cầu bổ sung, chờ cấp quản lý duyệt.',
      );
      await this.load();
    } catch (error: unknown) {
      this.yeuCauError = this.extractError(error);
    } finally {
      this.yeuCauLoading = false;
    }
  }

  snapshotStatusLabel(status: number): string {
    return this.snapshotStatusMap[status]?.label ?? '—';
  }

  snapshotStatusTone(status: number): BadgeTone {
    return this.snapshotStatusMap[status]?.tone ?? 'neutral';
  }

  private extractError(error: unknown): string {
    const r = (
      error as {
        error?: {
          error?: { message?: string; Message?: string };
          Error?: { message?: string; Message?: string };
        };
      }
    )?.error;
    return (
      r?.error?.message ??
      r?.error?.Message ??
      r?.Error?.message ??
      r?.Error?.Message ??
      'Không thể tải hoặc nộp báo cáo. Vui lòng thử lại.'
    );
  }
}
