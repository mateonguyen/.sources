import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { DropdownModule } from 'primeng/dropdown';
import { InputTextModule } from 'primeng/inputtext';
import { TableModule } from 'primeng/table';
import { TooltipModule } from 'primeng/tooltip';
import { NotificationService } from '../../core/ui/notification.service';
import { EmptyStateComponent } from '../../shared/ui/empty-state.component';
import { LoadingOverlayComponent } from '../../shared/ui/loading-overlay.component';
import { SectionCardComponent } from '../../shared/ui/section-card.component';
import { StatusBadgeComponent } from '../../shared/ui/status-badge.component';
import { KyBaoCaoApi, KyBaoCaoDto } from '../ky-bao-cao/ky-bao-cao.api';
import {
  SnapshotApi,
  SnapshotBreakdownDto,
  SnapshotCompareDto,
  SnapshotDto,
} from '../snapshot/snapshot.api';

type BadgeTone = 'neutral' | 'info' | 'success' | 'warning' | 'danger';

@Component({
  selector: 'app-tra-cuu-bao-cao-page',
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
    TableModule,
    TooltipModule,
  ],
  templateUrl: './tra-cuu-bao-cao.page.html',
  styleUrls: ['./tra-cuu-bao-cao.page.scss'],
})
export class TraCuuBaoCaoPage implements OnInit {
  loading = false;
  comparing = false;
  downloadingId: number | null = null;
  allKy: KyBaoCaoDto[] = [];
  selectedKyId: number | null = null;
  viewMode: 'latest' | 'byKy' = 'latest';
  allSnapshots: SnapshotDto[] = [];
  filterStatus: number | null = null;
  filterText = '';
  latestOnly = true;
  compareDonViId: number | null = null;
  compareFromKyId: number | null = null;
  compareToKyId: number | null = null;
  compareResult: SnapshotCompareDto | null = null;

  showBreakdownDialog = false;
  breakdownLoading = false;
  breakdownLoadingId: number | null = null;
  breakdown: SnapshotBreakdownDto | null = null;
  exportingId: number | null = null;

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
    ATTT_HTTT_VAN_HANH: 'ATTT vận hành',
    ATTT_HTTT_DAU_TU: 'ATTT đầu tư',
    ATTT_GIAI_PHAP: 'Giải pháp ATTT',
    VAN_BAN_QPPL: 'Văn bản QPPL',
  };

  readonly viewModeOptions = [
    { label: 'Theo báo cáo gần nhất từng đơn vị', value: 'latest' as const },
    { label: 'Theo một kỳ báo cáo cụ thể', value: 'byKy' as const },
  ];

  readonly statusOptions = [
    { label: 'Tất cả trạng thái', value: null },
    { label: 'Bản nháp', value: 1 },
    { label: 'Đã nộp', value: 2 },
    { label: 'Đã khoá', value: 3 },
    { label: 'Đã thay thế', value: 4 },
  ];

  readonly statusMap: Record<number, { label: string; tone: BadgeTone }> = {
    1: { label: 'Bản nháp', tone: 'neutral' },
    2: { label: 'Đã nộp', tone: 'info' },
    3: { label: 'Đã khoá', tone: 'success' },
    4: { label: 'Đã thay thế', tone: 'warning' },
  };

  constructor(
    private readonly kyBaoCaoApi: KyBaoCaoApi,
    private readonly snapshotApi: SnapshotApi,
    private readonly notification: NotificationService,
  ) {}

  ngOnInit(): void {
    void this.loadKy();
  }

  get kyOptions(): { label: string; value: number }[] {
    return this.allKy.map((k) => ({ label: k.tenKy || k.kyCode, value: k.id }));
  }

  get donViOptions(): { label: string; value: number }[] {
    const seen = new Set<number>();
    return this.allSnapshots
      .filter((x) => {
        if (seen.has(x.donViId)) return false;
        seen.add(x.donViId);
        return true;
      })
      .map((x) => ({ label: x.tenDonVi, value: x.donViId }))
      .sort((a, b) => a.label.localeCompare(b.label, 'vi'));
  }

  get filteredSnapshots(): SnapshotDto[] {
    let rows = this.allSnapshots;

    if (this.latestOnly) {
      const seen = new Set<number>();
      rows = rows.filter((r) => {
        if (seen.has(r.donViId)) return false;
        seen.add(r.donViId);
        return true;
      });
    }

    if (this.filterStatus !== null) {
      rows = rows.filter((r) => r.trangThai === this.filterStatus);
    }

    const q = this.filterText.trim().toLowerCase();
    if (q) {
      rows = rows.filter(
        (r) =>
          r.tenDonVi.toLowerCase().includes(q) ||
          r.kyCode.toLowerCase().includes(q) ||
          String(r.donViId).includes(q),
      );
    }

    return rows;
  }

  statusLabel(trangThai: number): string {
    return this.statusMap[trangThai]?.label ?? `#${trangThai}`;
  }

  statusTone(trangThai: number): BadgeTone {
    return this.statusMap[trangThai]?.tone ?? 'neutral';
  }

  async loadKy(): Promise<void> {
    this.loading = true;
    try {
      this.allKy = await this.kyBaoCaoApi.getAll();
      if (this.allKy.length > 0) {
        const moKy = this.allKy.find((k) => k.trangThai === 2);
        this.selectedKyId = (moKy ?? this.allKy[0]).id;
        this.compareFromKyId = this.selectedKyId;
        this.compareToKyId = this.allKy[1]?.id ?? this.selectedKyId;
        await this.loadSnapshots();
      }
    } catch {
      this.notification.show('error', 'Không thể tải danh sách kỳ báo cáo.');
    } finally {
      this.loading = false;
    }
  }

  async loadSnapshots(): Promise<void> {
    this.loading = true;
    try {
      if (this.viewMode === 'latest') {
        this.allSnapshots = await this.snapshotApi.getLatestByDonVi();
        this.latestOnly = true;
      } else {
        if (!this.selectedKyId) {
          this.allSnapshots = [];
          return;
        }
        this.allSnapshots = await this.snapshotApi.getByKy(this.selectedKyId);
      }
      if (!this.compareDonViId && this.allSnapshots.length > 0) {
        this.compareDonViId = this.allSnapshots[0].donViId;
      }
    } catch {
      this.notification.show('error', 'Không thể tải danh sách báo cáo.');
    } finally {
      this.loading = false;
    }
  }

  get breakdownChildren() {
    return this.breakdown?.children ?? [];
  }

  moduleLabel(code: string): string {
    return this.moduleLabels[code] || code;
  }

  /** Tổng bản ghi của 1 đơn vị con trong breakdown. */
  breakdownUnitTotal(unit: { moduleCounts: { recordCount: number }[] }): number {
    return unit.moduleCounts.reduce(
      (sum, m) => sum + Math.max(m.recordCount, 0),
      0,
    );
  }

  async openBreakdown(snapshot: SnapshotDto): Promise<void> {
    this.breakdownLoadingId = snapshot.id;
    this.breakdownLoading = true;
    this.breakdown = null;
    this.showBreakdownDialog = true;
    try {
      this.breakdown = await this.snapshotApi.getBreakdown(snapshot.id);
    } catch {
      this.showBreakdownDialog = false;
      this.notification.show(
        'error',
        'Không thể tải chi tiết đóng góp của báo cáo này.',
      );
    } finally {
      this.breakdownLoading = false;
      this.breakdownLoadingId = null;
    }
  }

  closeBreakdown(): void {
    this.showBreakdownDialog = false;
    this.breakdown = null;
  }

  async downloadPdf(snapshot: SnapshotDto): Promise<void> {
    this.downloadingId = snapshot.id;
    try {
      // PDF biểu mẫu (mẫu H05) — convert từ chính file Excel export
      const result = await this.snapshotApi.getExport(snapshot.id, 'pdf');
      window.open(result.downloadUrl, '_blank');
    } catch {
      this.notification.show(
        'error',
        'Không thể tạo file PDF, vui lòng thử lại.',
      );
    } finally {
      this.downloadingId = null;
    }
  }

  /** Tải biểu mẫu báo cáo (Excel theo mẫu H05) từ dữ liệu đã chốt. */
  async downloadExport(snapshot: SnapshotDto, format: 'xlsx' | 'pdf'): Promise<void> {
    this.exportingId = snapshot.id;
    try {
      const result = await this.snapshotApi.getExport(snapshot.id, format);
      window.open(result.downloadUrl, '_blank');
    } catch {
      this.notification.show(
        'error',
        'Không thể xuất biểu mẫu, vui lòng thử lại.',
      );
    } finally {
      this.exportingId = null;
    }
  }

  onKyChange(): void {
    this.compareResult = null;
    void this.loadSnapshots();
  }

  onViewModeChange(): void {
    this.compareResult = null;
    void this.loadSnapshots();
  }

  async compareTwoKy(): Promise<void> {
    if (!this.compareDonViId || !this.compareFromKyId || !this.compareToKyId) {
      this.notification.show(
        'warning',
        'Vui lòng chọn đơn vị và 2 kỳ báo cáo để so sánh.',
      );
      return;
    }

    if (this.compareFromKyId === this.compareToKyId) {
      this.notification.show(
        'warning',
        'Hai kỳ báo cáo so sánh phải khác nhau.',
      );
      return;
    }

    this.comparing = true;
    try {
      this.compareResult = await this.snapshotApi.compareTwoKy(
        this.compareDonViId,
        this.compareFromKyId,
        this.compareToKyId,
      );
    } catch {
      this.compareResult = null;
      this.notification.show(
        'error',
        'Không thể so sánh 2 kỳ báo cáo của đơn vị đã chọn.',
      );
    } finally {
      this.comparing = false;
    }
  }
}
