import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { DropdownModule } from 'primeng/dropdown';
import { TableModule } from 'primeng/table';
import { TooltipModule } from 'primeng/tooltip';
import { KyBaoCaoApi, KyBaoCaoDto } from '../ky-bao-cao/ky-bao-cao.api';
import {
  ChiTietModuleDto,
  TienDoDonViDto,
  TongHopTienDoApi,
} from '../tong-hop-tien-do/tong-hop-tien-do.api';
import { EmptyStateComponent } from '../../shared/ui/empty-state.component';
import { LoadingOverlayComponent } from '../../shared/ui/loading-overlay.component';
import { SectionCardComponent } from '../../shared/ui/section-card.component';
import { StatusBadgeComponent } from '../../shared/ui/status-badge.component';
import { NotificationService } from '../../core/ui/notification.service';

@Component({
  selector: 'app-tien-do-tong-hop-page',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    SectionCardComponent,
    EmptyStateComponent,
    LoadingOverlayComponent,
    ButtonModule,
    DialogModule,
    DropdownModule,
    TableModule,
    TooltipModule,
    StatusBadgeComponent,
  ],
  templateUrl: './tien-do-tong-hop.page.html',
  styleUrls: ['./tien-do-tong-hop.page.scss'],
})
export class TienDoTongHopPage implements OnInit {
  loading = false;
  loadingData = false;
  allKy: KyBaoCaoDto[] = [];
  selectedKyCode: string | null = null;
  rows: TienDoDonViDto[] = [];

  showDetailDialog = false;
  detailUnit: TienDoDonViDto | null = null;
  detailRecords: ChiTietModuleDto | null = null;
  detailRecordsLoading = false;
  selectedModuleCode: string | null = null;

  /** 17 module nghiệp vụ — code khớp mẫu báo cáo, key khớp field TienDoDonViDto. */
  readonly moduleDefs: Array<{
    code: string;
    label: string;
    key: keyof TienDoDonViDto;
  }> = [
    { code: 'NHAN_LUC_CNTT', label: 'Nhân lực CNTT', key: 'soNhanLuc' },
    { code: 'NANG_LUC_SO', label: 'Năng lực số', key: 'soNangLucSo' },
    { code: 'DAO_TAO_BOI_DUONG', label: 'Đào tạo bồi dưỡng', key: 'soDaoTao' },
    { code: 'DAO_TAO_HOC_VIEN', label: 'Đào tạo học viện', key: 'soDaoTaoHocVien' },
    { code: 'HE_THONG_THONG_TIN', label: 'Hệ thống thông tin', key: 'soHeThongThongTin' },
    { code: 'HTTT_TIEU_CHUAN', label: 'HTTT tiêu chuẩn', key: 'soHtttTieuChuan' },
    { code: 'DU_AN_CNTT', label: 'Dự án CNTT', key: 'soDuAn' },
    { code: 'THIET_BI_CNTT', label: 'Thiết bị CNTT', key: 'soThietBi' },
    { code: 'HA_TANG_MANG', label: 'Hạ tầng mạng', key: 'soHaTangMang' },
    { code: 'GIAM_SAT_NOC', label: 'Giám sát NOC', key: 'soGiamSatNoc' },
    { code: 'CAMERA_QUAN_LY', label: 'Camera quản lý', key: 'soCameraQuanLy' },
    { code: 'CAMERA_THUC_TRANG', label: 'Camera thực trạng', key: 'soCameraThucTrang' },
    { code: 'GIAM_SAT_SOC', label: 'Giám sát SOC', key: 'soGiamSatSoc' },
    { code: 'ATTT_HTTT_VAN_HANH', label: 'ATTT vận hành', key: 'soAtttVanHanh' },
    { code: 'ATTT_HTTT_DAU_TU', label: 'ATTT đầu tư', key: 'soAtttDauTu' },
    { code: 'ATTT_GIAI_PHAP', label: 'Giải pháp ATTT', key: 'soAtttGiaiPhap' },
    { code: 'VAN_BAN_QPPL', label: 'Văn bản QPPL', key: 'soVanBanQppl' },
  ];

  constructor(
    private readonly kyBaoCaoApi: KyBaoCaoApi,
    private readonly tongHopApi: TongHopTienDoApi,
    private readonly notification: NotificationService,
  ) {}

  ngOnInit(): void {
    void this.loadKy();
  }

  get kyOptions(): { label: string; value: string }[] {
    const sorted = [...this.allKy].sort((a, b) => {
      if (a.trangThai === 2 && b.trangThai !== 2) return -1;
      if (a.trangThai !== 2 && b.trangThai === 2) return 1;
      return 0;
    });
    return sorted.map((k) => ({ label: k.tenKy || k.kyCode, value: k.kyCode }));
  }

  get selectedKy(): KyBaoCaoDto | null {
    return this.allKy.find((k) => k.kyCode === this.selectedKyCode) ?? null;
  }

  get soXacNhan(): number {
    return this.rows.filter((r) => r.daXacNhan).length;
  }

  get soCoDuLieu(): number {
    return this.rows.filter((r) => this.totalRecords(r) > 0).length;
  }

  get soChuaCoDuLieu(): number {
    return this.rows.length - this.soCoDuLieu;
  }

  totalRecords(row: TienDoDonViDto): number {
    return (
      (row.soNhanLuc ?? 0) +
      (row.soNangLucSo ?? 0) +
      (row.soDaoTao ?? 0) +
      (row.soDaoTaoHocVien ?? 0) +
      (row.soHeThongThongTin ?? 0) +
      (row.soHtttTieuChuan ?? 0) +
      (row.soDuAn ?? 0) +
      (row.soThietBi ?? 0) +
      (row.soHaTangMang ?? 0) +
      (row.soGiamSatNoc ?? 0) +
      (row.soCameraQuanLy ?? 0) +
      (row.soCameraThucTrang ?? 0) +
      (row.soGiamSatSoc ?? 0) +
      (row.soAtttVanHanh ?? 0) +
      (row.soAtttDauTu ?? 0) +
      (row.soAtttGiaiPhap ?? 0) +
      (row.soVanBanQppl ?? 0)
    );
  }

  /** Chỉ các module có trong mẫu báo cáo của kỳ đang chọn (mẫu trống = đủ 17). */
  get detailModules(): Array<{ code: string; label: string; count: number }> {
    if (!this.detailUnit) return [];
    const kyModules = this.selectedKy?.moduleList ?? [];
    const defs =
      kyModules.length > 0
        ? this.moduleDefs.filter((def) => kyModules.includes(def.code))
        : this.moduleDefs;
    return defs.map((def) => ({
      code: def.code,
      label: def.label,
      count: (this.detailUnit![def.key] as number) ?? 0,
    }));
  }

  get detailEmptyModuleCount(): number {
    return this.detailModules.filter((m) => m.count === 0).length;
  }

  openDetail(row: TienDoDonViDto): void {
    this.detailUnit = row;
    this.detailRecords = null;
    this.selectedModuleCode = null;
    this.showDetailDialog = true;

    // Tự mở module đầu tiên có dữ liệu cho đỡ phải bấm thêm
    const first = this.detailModules.find((m) => m.count > 0);
    if (first) void this.loadDetailRecords(first.code);
  }

  async loadDetailRecords(moduleCode: string): Promise<void> {
    if (!this.detailUnit) return;
    this.selectedModuleCode = moduleCode;
    this.detailRecordsLoading = true;
    this.detailRecords = null;
    try {
      this.detailRecords = await this.tongHopApi.getChiTietModule(
        this.detailUnit.donViId,
        moduleCode,
      );
    } catch {
      this.notification.show(
        'error',
        'Không thể tải chi tiết bản ghi của module này.',
      );
    } finally {
      this.detailRecordsLoading = false;
    }
  }

  detailCellValue(row: Record<string, unknown>, key: string): string {
    const value = row[key];
    if (value === null || value === undefined || value === '') return '—';
    return String(value);
  }

  closeDetail(): void {
    this.showDetailDialog = false;
    this.detailUnit = null;
    this.detailRecords = null;
    this.selectedModuleCode = null;
  }

  async loadKy(): Promise<void> {
    this.loading = true;
    try {
      this.allKy = await this.kyBaoCaoApi.getAll();
      if (this.allKy.length > 0) {
        const dangMo = this.allKy.find((k) => k.trangThai === 2);
        this.selectedKyCode = (dangMo ?? this.allKy[0]).kyCode;
        await this.loadData();
      }
    } catch {
      this.notification.show('error', 'Không thể tải danh sách kỳ báo cáo.');
    } finally {
      this.loading = false;
    }
  }

  async loadData(): Promise<void> {
    if (!this.selectedKyCode) return;
    this.loadingData = true;
    try {
      this.rows = await this.tongHopApi.getTienDo(this.selectedKyCode);
    } catch {
      this.notification.show(
        'error',
        'Không thể tải dữ liệu tiến độ tổng hợp.',
      );
    } finally {
      this.loadingData = false;
    }
  }
}
