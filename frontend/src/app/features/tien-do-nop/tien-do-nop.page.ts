import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { PrimeNGConfig } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CalendarModule } from 'primeng/calendar';
import { DialogModule } from 'primeng/dialog';
import { DropdownModule } from 'primeng/dropdown';
import { InputTextareaModule } from 'primeng/inputtextarea';
import { TableModule } from 'primeng/table';
import { NotificationService } from '../../core/ui/notification.service';
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
import { YeuCauBoSungApi } from '../yeu-cau-bo-sung/yeu-cau-bo-sung.api';

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
    CalendarModule,
    InputTextareaModule,
    TableModule,
  ],
  templateUrl: './tien-do-nop.page.html',
  styleUrls: ['./tien-do-nop.page.scss'],
})
export class TienDoNopPage implements OnInit {
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
  readonly ycbsMinDate = new Date();

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
    private readonly notificationService: NotificationService,
    private readonly route: ActivatedRoute,
    private readonly primeNgConfig: PrimeNGConfig,
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

  get kyOptions(): { label: string; value: number }[] {
    const sorted = [...this.allKy].sort((a, b) => {
      if (a.trangThai === 2 && b.trangThai !== 2) return -1;
      if (a.trangThai !== 2 && b.trangThai === 2) return 1;
      return 0;
    });
    return sorted.map((k) => ({ label: k.tenKy || k.kyCode, value: k.id }));
  }

  get selectedKy(): KyBaoCaoDto | null {
    return this.allKy.find((k) => k.id === this.selectedKyId) ?? null;
  }

  get tienDoRows(): KyBaoCaoTienDoDonViDto[] {
    return this.tienDo?.danhSachDonVi ?? this.tienDo?.donVis ?? [];
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
      return;
    }

    this.loadingTienDo = true;
    try {
      this.tienDo = await this.kyBaoCaoApi.getTienDo(this.selectedKyId);
    } catch (error: unknown) {
      this.tienDo = null;
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
}
