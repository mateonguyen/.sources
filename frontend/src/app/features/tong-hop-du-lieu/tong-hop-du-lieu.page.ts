import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
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
import { ConfirmDialogWrapperService } from '../../shared/ui/confirm-dialog-wrapper.service';
import { DonViModeService } from '../../core/don-vi/don-vi-mode.service';

interface ModuleStat {
  label: string;
  route: string;
  count: number;
  icon: string;
}

interface ModuleDef {
  label: string;
  route: string;
  icon: string;
  countKey: keyof TienDoDonViDto;
}

/**
 * Gộp "Tổng hợp dữ liệu" + "Tiến độ tổng hợp" thành 1 trang duy nhất, nội dung
 * branch nghiêm theo CheDoNhapLieu của đơn vị (giống Trang chủ): Tự nhập chỉ
 * thấy dữ liệu module của chính mình; Tổng hợp chỉ thấy tiến độ đơn vị cấp dưới.
 * Không hiển thị lẫn để tránh gây nhiễu.
 */
@Component({
  selector: 'app-tong-hop-du-lieu-page',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    SectionCardComponent,
    EmptyStateComponent,
    LoadingOverlayComponent,
    StatusBadgeComponent,
    ButtonModule,
    DialogModule,
    DropdownModule,
    TableModule,
    TooltipModule,
  ],
  templateUrl: './tong-hop-du-lieu.page.html',
  styleUrls: ['./tong-hop-du-lieu.page.scss'],
})
export class TongHopDuLieuPage implements OnInit {
  loading = false;
  loadingData = false;
  saving = false;
  allKy: KyBaoCaoDto[] = [];
  selectedKyCode: string | null = null;

  // ── Tự nhập: dữ liệu của chính đơn vị mình ──
  myTienDo: TienDoDonViDto | null = null;
  showXacNhanDialog = false;
  xacNhanCountdown = 5;
  private xacNhanTimer: ReturnType<typeof setInterval> | null = null;

  // ── Tổng hợp: tiến độ các đơn vị cấp dưới ──
  rows: TienDoDonViDto[] = [];
  showDetailDialog = false;
  detailUnit: TienDoDonViDto | null = null;
  detailRecords: ChiTietModuleDto | null = null;
  detailRecordsLoading = false;
  selectedModuleCode: string | null = null;

  constructor(
    private readonly kyBaoCaoApi: KyBaoCaoApi,
    private readonly tongHopApi: TongHopTienDoApi,
    private readonly notification: NotificationService,
    private readonly confirmDialog: ConfirmDialogWrapperService,
    readonly donViModeService: DonViModeService,
  ) {}

  ngOnInit(): void {
    void this.donViModeService.ensureLoaded();
    void this.loadKy();
  }

  get isTongHop(): boolean {
    return this.donViModeService.isTongHop;
  }

  get kyOptions(): { label: string; value: string }[] {
    // DangMo (trangThai=2) first, then others
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

  get selectedKyDisplayName(): string {
    const ky = this.selectedKy;
    return ky ? (ky.tenKy || ky.kyCode) : (this.selectedKyCode ?? '');
  }

  async loadKy(): Promise<void> {
    this.loading = true;
    try {
      this.allKy = await this.kyBaoCaoApi.getAll();
      if (this.allKy.length > 0) {
        const dangMo = this.allKy.find((k) => k.trangThai === 2);
        this.selectedKyCode = (dangMo ?? this.allKy[0]).kyCode;
        await this.onKyChange();
      }
    } catch {
      this.notification.show('error', 'Không thể tải danh sách kỳ báo cáo.');
    } finally {
      this.loading = false;
    }
  }

  async onKyChange(): Promise<void> {
    await this.donViModeService.ensureLoaded();
    if (this.isTongHop) {
      await this.loadData();
    } else {
      await this.loadMyTienDo();
    }
  }

  // ══════════════════════ Tự nhập ══════════════════════

  get pendingYcbsDeadline(): string | null {
    return this.myTienDo?.hanBoSung ?? null;
  }

  /** pending = chưa xác nhận; stale = đã xác nhận nhưng số liệu đổi sau đó; confirmed = xanh */
  get xacNhanState(): 'pending' | 'stale' | 'confirmed' {
    if (!this.myTienDo?.daXacNhan) return 'pending';
    return this.myTienDo.coThayDoiSauXacNhan ? 'stale' : 'confirmed';
  }

  readonly MODULE_MAP: Record<string, ModuleDef> = {
    NHAN_LUC_CNTT:      { label: 'Nhân lực CNTT',          route: '/nhan-luc-cntt',        icon: 'pi pi-users',         countKey: 'soNhanLuc' },
    NANG_LUC_SO:        { label: 'Năng lực số',             route: '/nang-luc-so',          icon: 'pi pi-star',          countKey: 'soNangLucSo' },
    DAO_TAO_BOI_DUONG:  { label: 'Đào tạo bồi dưỡng',      route: '/dao-tao-boi-duong',    icon: 'pi pi-graduation-cap',countKey: 'soDaoTao' },
    DAO_TAO_HOC_VIEN:   { label: 'Đào tạo học viện',        route: '/dao-tao-hoc-vien',     icon: 'pi pi-book',          countKey: 'soDaoTaoHocVien' },
    HE_THONG_THONG_TIN: { label: 'Hệ thống thông tin',      route: '/he-thong-thong-tin',   icon: 'pi pi-server',        countKey: 'soHeThongThongTin' },
    HTTT_TIEU_CHUAN:    { label: 'HTTT tiêu chuẩn',         route: '/httt-tieu-chuan',      icon: 'pi pi-check-square',  countKey: 'soHtttTieuChuan' },
    DU_AN_CNTT:         { label: 'Dự án CNTT',              route: '/du-an-cntt',           icon: 'pi pi-folder',        countKey: 'soDuAn' },
    THIET_BI_CNTT:      { label: 'Thiết bị CNTT',           route: '/thiet-bi-cntt',        icon: 'pi pi-desktop',       countKey: 'soThietBi' },
    HA_TANG_MANG:       { label: 'Hạ tầng mạng',            route: '/ha-tang-mang',         icon: 'pi pi-wifi',          countKey: 'soHaTangMang' },
    GIAM_SAT_NOC:       { label: 'Giám sát NOC',            route: '/giam-sat-noc',         icon: 'pi pi-eye',           countKey: 'soGiamSatNoc' },
    CAMERA_QUAN_LY:     { label: 'Camera quản lý',          route: '/camera-quan-ly',       icon: 'pi pi-video',         countKey: 'soCameraQuanLy' },
    CAMERA_THUC_TRANG:  { label: 'Camera thực trạng',       route: '/camera-thuc-trang',    icon: 'pi pi-camera',        countKey: 'soCameraThucTrang' },
    GIAM_SAT_SOC:       { label: 'Giám sát SOC',            route: '/giam-sat-soc',         icon: 'pi pi-shield',        countKey: 'soGiamSatSoc' },
    ATTT_HTTT_VAN_HANH: { label: 'ATTT hệ thống vận hành',  route: '/attt-httt-van-hanh',   icon: 'pi pi-lock',          countKey: 'soAtttVanHanh' },
    ATTT_HTTT_DAU_TU:   { label: 'ATTT hệ thống đầu tư',    route: '/attt-httt-dau-tu',     icon: 'pi pi-lock-open',     countKey: 'soAtttDauTu' },
    ATTT_GIAI_PHAP:     { label: 'Giải pháp ATTT',          route: '/giai-phap-attt',       icon: 'pi pi-verified',      countKey: 'soAtttGiaiPhap' },
    VAN_BAN_QPPL:       { label: 'Văn bản QPPL',            route: '/van-ban-qppl',         icon: 'pi pi-file',          countKey: 'soVanBanQppl' },
  };

  get modulStats(): ModuleStat[] {
    if (!this.myTienDo) return [];
    const moduleList = this.selectedKy?.moduleList;
    const codes = (moduleList && moduleList.length > 0)
      ? moduleList
      : Object.keys(this.MODULE_MAP);
    return codes
      .filter((code) => code in this.MODULE_MAP)
      .map((code) => {
        const def = this.MODULE_MAP[code];
        return {
          label: def.label,
          route: def.route,
          icon: def.icon,
          count: (this.myTienDo![def.countKey] as number) ?? 0,
        };
      });
  }

  async loadMyTienDo(): Promise<void> {
    if (!this.selectedKyCode) return;
    this.loading = true;
    try {
      this.myTienDo = await this.tongHopApi.getMyTienDo(this.selectedKyCode);
    } catch {
      this.notification.show('error', 'Không thể tải dữ liệu tổng hợp.');
    } finally {
      this.loading = false;
    }
  }

  openXacNhanDialog(): void {
    this.showXacNhanDialog = true;
    this.xacNhanCountdown = 5;
    this.xacNhanTimer = setInterval(() => {
      this.xacNhanCountdown--;
      if (this.xacNhanCountdown <= 0) {
        clearInterval(this.xacNhanTimer!);
        this.xacNhanTimer = null;
      }
    }, 1000);
  }

  closeXacNhanDialog(): void {
    this.showXacNhanDialog = false;
    if (this.xacNhanTimer) {
      clearInterval(this.xacNhanTimer);
      this.xacNhanTimer = null;
    }
  }

  async doXacNhan(): Promise<void> {
    this.closeXacNhanDialog();
    await this.setXacNhan(true);
  }

  async confirmMoLai(): Promise<void> {
    const ok = await this.confirmDialog.confirmReopen({
      header: 'Hủy xác nhận',
      message:
        'Hủy trạng thái "đã xác nhận" để cấp trên biết số liệu chưa chốt? Bạn vẫn nhập liệu bình thường, xong thì bấm xác nhận lại.',
      acceptLabel: 'Hủy xác nhận',
    });
    if (ok) await this.setXacNhan(false);
  }

  private async setXacNhan(value: boolean): Promise<void> {
    if (!this.selectedKyCode) return;
    this.saving = true;
    try {
      await this.tongHopApi.xacNhan(this.selectedKyCode, value);
      this.notification.show(
        'success',
        value
          ? 'Đã xác nhận hoàn tất khai báo.'
          : 'Đã hủy xác nhận — cấp trên sẽ thấy số liệu chưa chốt.',
      );
      await this.loadMyTienDo();
    } catch {
      this.notification.show('error', 'Thao tác thất bại, vui lòng thử lại.');
    } finally {
      this.saving = false;
    }
  }

  // ══════════════════════ Tổng hợp ══════════════════════

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
