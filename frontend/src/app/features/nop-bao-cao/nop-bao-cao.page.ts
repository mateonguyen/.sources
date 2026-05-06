import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextareaModule } from 'primeng/inputtextarea';
import { TableModule } from 'primeng/table';
import { AuthService } from '../../core/auth/auth.service';
import { NotificationService } from '../../core/ui/notification.service';
import { EmptyStateComponent } from '../../shared/ui/empty-state.component';
import { LoadingOverlayComponent } from '../../shared/ui/loading-overlay.component';
import { SectionCardComponent } from '../../shared/ui/section-card.component';
import { StatusBadgeComponent } from '../../shared/ui/status-badge.component';
import { KyBaoCaoApi, KyBaoCaoDto } from '../ky-bao-cao/ky-bao-cao.api';
import { SnapshotApi, SnapshotDto } from '../snapshot/snapshot.api';
import {
  YeuCauBoSungApi,
  YeuCauBoSungDto,
} from '../yeu-cau-bo-sung/yeu-cau-bo-sung.api';

type BadgeTone = 'neutral' | 'info' | 'success' | 'warning' | 'danger';

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
    DialogModule,
    InputTextareaModule,
    TableModule,
  ],
  templateUrl: './nop-bao-cao.page.html',
  styleUrls: ['./nop-bao-cao.page.scss'],
})
export class NopBaoCaoPage implements OnInit {
  loading = false;
  currentKy: KyBaoCaoDto | null = null;
  ownSnapshots: SnapshotDto[] = [];
  pendingYeuCau: YeuCauBoSungDto[] = [];
  showNopDialog = false;
  ghiChuNop = '';
  nopLoading = false;
  nopError = '';

  readonly snapshotStatusMap: Record<
    number,
    { label: string; tone: BadgeTone }
  > = {
    1: { label: 'Bản nháp', tone: 'neutral' },
    2: { label: 'Đã nộp', tone: 'success' },
    3: { label: 'Đã khóa', tone: 'warning' },
    4: { label: 'Đã thay thế', tone: 'neutral' },
  };

  readonly submissionStatusMap: Record<
    number,
    { label: string; tone: BadgeTone }
  > = {
    0: { label: 'Chưa nộp', tone: 'neutral' },
    1: { label: 'Bản nháp', tone: 'neutral' },
    2: { label: 'Đã nộp', tone: 'success' },
    3: { label: 'Đã khóa', tone: 'warning' },
    4: { label: 'Đang bổ sung', tone: 'warning' },
  };

  constructor(
    private readonly kyBaoCaoApi: KyBaoCaoApi,
    private readonly snapshotApi: SnapshotApi,
    private readonly yeuCauApi: YeuCauBoSungApi,
    private readonly authService: AuthService,
    private readonly notificationService: NotificationService,
  ) {}

  ngOnInit(): void {
    void this.load();
  }

  get currentDonViId(): number {
    return this.authService.profile()?.donViId ?? 0;
  }

  get latestSnapshot(): SnapshotDto | null {
    return this.ownSnapshots[0] ?? null;
  }

  get canSubmit(): boolean {
    return !!(
      this.currentKy?.trangThai === 2 &&
      this.currentDonViId > 0 &&
      (this.latestSnapshot === null || this.latestSnapshot.trangThai !== 3)
    );
  }

  get hasPendingYeuCau(): boolean {
    return this.pendingYeuCau.length > 0;
  }

  get submissionStatusLabel(): string {
    return (
      this.submissionStatusMap[this.latestSnapshot?.trangThai ?? 0]?.label ??
      'Chưa nộp'
    );
  }

  get submissionStatusTone(): BadgeTone {
    return (
      this.submissionStatusMap[this.latestSnapshot?.trangThai ?? 0]?.tone ??
      'neutral'
    );
  }

  async load(): Promise<void> {
    this.loading = true;
    try {
      this.currentKy = await this.kyBaoCaoApi.getCurrent().catch(() => null);
      this.ownSnapshots = [];
      this.pendingYeuCau = [];

      if (!this.currentKy) {
        return;
      }

      const [snapshots, yeuCau] = await Promise.all([
        this.snapshotApi.getByKy(this.currentKy.id),
        this.yeuCauApi.getByKy(this.currentKy.id),
      ]);

      this.ownSnapshots = snapshots
        .filter((item) => item.donViId === this.currentDonViId)
        .sort((a, b) => (b.phienBan ?? 0) - (a.phienBan ?? 0));

      this.pendingYeuCau = yeuCau.filter(
        (item) => item.donViId === this.currentDonViId && item.trangThai <= 2,
      );
    } catch (error: unknown) {
      this.notificationService.show('error', this.extractError(error));
    } finally {
      this.loading = false;
    }
  }

  openNopDialog(): void {
    this.ghiChuNop = '';
    this.nopError = '';
    this.showNopDialog = true;
  }

  closeNopDialog(): void {
    this.showNopDialog = false;
  }

  async confirmNop(): Promise<void> {
    if (!this.currentKy) {
      return;
    }

    this.nopLoading = true;
    this.nopError = '';

    try {
      await this.snapshotApi.submitCurrent({
        kyBaoCaoId: this.currentKy.id,
        donViId: this.currentDonViId,
        ghiChu: this.ghiChuNop.trim() || undefined,
      });

      this.showNopDialog = false;
      this.notificationService.show(
        'success',
        `Đã nộp báo cáo kỳ ${this.currentKy.kyCode} thành công.`,
      );
      await this.load();
    } catch (error: unknown) {
      this.nopError = this.extractError(error);
    } finally {
      this.nopLoading = false;
    }
  }

  snapshotStatusLabel(status: number): string {
    return this.snapshotStatusMap[status]?.label ?? '—';
  }

  snapshotStatusTone(status: number): BadgeTone {
    return this.snapshotStatusMap[status]?.tone ?? 'neutral';
  }

  private extractError(error: unknown): string {
    return (
      (error as { error?: { error?: { message?: string } } })?.error?.error
        ?.message ?? 'Không thể tải hoặc nộp báo cáo. Vui lòng thử lại.'
    );
  }
}
