import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { DropdownModule } from 'primeng/dropdown';
import { KyBaoCaoApi, KyBaoCaoDto } from '../ky-bao-cao/ky-bao-cao.api';
import {
  TienDoDonViDto,
  TongHopTienDoApi,
} from '../tong-hop-tien-do/tong-hop-tien-do.api';
import { EmptyStateComponent } from '../../shared/ui/empty-state.component';
import { LoadingOverlayComponent } from '../../shared/ui/loading-overlay.component';
import { SectionCardComponent } from '../../shared/ui/section-card.component';
import { NotificationService } from '../../core/ui/notification.service';
import { ConfirmDialogWrapperService } from '../../shared/ui/confirm-dialog-wrapper.service';

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
    ButtonModule,
    DialogModule,
    DropdownModule,
  ],
  templateUrl: './tong-hop-du-lieu.page.html',
  styleUrls: ['./tong-hop-du-lieu.page.scss'],
})
export class TongHopDuLieuPage implements OnInit {
  loading = false;
  saving = false;
  allKy: KyBaoCaoDto[] = [];
  selectedKyCode: string | null = null;
  myTienDo: TienDoDonViDto | null = null;

  showXacNhanDialog = false;
  xacNhanCountdown = 5;
  private xacNhanTimer: ReturnType<typeof setInterval> | null = null;

  constructor(
    private readonly kyBaoCaoApi: KyBaoCaoApi,
    private readonly tongHopApi: TongHopTienDoApi,
    private readonly notification: NotificationService,
    private readonly confirmDialog: ConfirmDialogWrapperService,
  ) {}

  ngOnInit(): void {
    void this.loadKy();
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

  async loadKy(): Promise<void> {
    this.loading = true;
    try {
      this.allKy = await this.kyBaoCaoApi.getAll();
      if (this.allKy.length > 0) {
        const dangMo = this.allKy.find((k) => k.trangThai === 2);
        this.selectedKyCode = (dangMo ?? this.allKy[0]).kyCode;
        await this.loadMyTienDo();
      }
    } catch {
      this.notification.show('error', 'Không thể tải danh sách kỳ báo cáo.');
    } finally {
      this.loading = false;
    }
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
}
