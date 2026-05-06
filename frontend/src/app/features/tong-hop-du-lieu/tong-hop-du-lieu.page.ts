import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { DropdownModule } from 'primeng/dropdown';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ConfirmationService } from 'primeng/api';
import { KyBaoCaoApi, KyBaoCaoDto } from '../ky-bao-cao/ky-bao-cao.api';
import {
  TienDoDonViDto,
  TongHopTienDoApi,
} from '../tong-hop-tien-do/tong-hop-tien-do.api';
import { EmptyStateComponent } from '../../shared/ui/empty-state.component';
import { LoadingOverlayComponent } from '../../shared/ui/loading-overlay.component';
import { SectionCardComponent } from '../../shared/ui/section-card.component';
import { NotificationService } from '../../core/ui/notification.service';

interface ModuleStat {
  label: string;
  route: string;
  count: number;
  icon: string;
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
    DropdownModule,
    ConfirmDialogModule,
  ],
  providers: [ConfirmationService],
  templateUrl: './tong-hop-du-lieu.page.html',
  styleUrls: ['./tong-hop-du-lieu.page.scss'],
})
export class TongHopDuLieuPage implements OnInit {
  loading = false;
  saving = false;
  allKy: KyBaoCaoDto[] = [];
  selectedKyCode: string | null = null;
  myTienDo: TienDoDonViDto | null = null;

  constructor(
    private readonly kyBaoCaoApi: KyBaoCaoApi,
    private readonly tongHopApi: TongHopTienDoApi,
    private readonly notification: NotificationService,
    private readonly confirmationService: ConfirmationService,
  ) {}

  ngOnInit(): void {
    void this.loadKy();
  }

  get kyOptions(): { label: string; value: string }[] {
    return this.allKy.map((k) => ({ label: k.kyCode, value: k.kyCode }));
  }

  get modulStats(): ModuleStat[] {
    if (!this.myTienDo) return [];
    return [
      {
        label: 'Nhân lực CNTT',
        route: '/nhan-luc-cntt',
        count: this.myTienDo.soNhanLuc,
        icon: 'pi pi-users',
      },
      {
        label: 'Thiết bị CNTT',
        route: '/thiet-bi-cntt',
        count: this.myTienDo.soThietBi,
        icon: 'pi pi-desktop',
      },
      {
        label: 'Hệ thống thông tin',
        route: '/he-thong-thong-tin',
        count: this.myTienDo.soHeThongThongTin,
        icon: 'pi pi-server',
      },
      {
        label: 'Hạ tầng mạng',
        route: '/ha-tang-mang',
        count: this.myTienDo.soHaTangMang,
        icon: 'pi pi-wifi',
      },
      {
        label: 'Đào tạo',
        route: '/dao-tao-boi-duong',
        count: this.myTienDo.soDaoTao,
        icon: 'pi pi-graduation-cap',
      },
      {
        label: 'Dự án CNTT',
        route: '/du-an-cntt',
        count: this.myTienDo.soDuAn,
        icon: 'pi pi-folder',
      },
    ];
  }

  async loadKy(): Promise<void> {
    this.loading = true;
    try {
      this.allKy = await this.kyBaoCaoApi.getAll();
      if (this.allKy.length > 0) {
        this.selectedKyCode = this.allKy[0].kyCode;
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

  confirmXacNhan(): void {
    this.confirmationService.confirm({
      message: 'Bạn xác nhận đã hoàn tất khai báo số liệu cho kỳ này?',
      header: 'Xác nhận hoàn tất',
      icon: 'pi pi-check-circle',
      acceptLabel: 'Xác nhận',
      rejectLabel: 'Hủy',
      accept: () => void this.setXacNhan(true),
    });
  }

  confirmMoLai(): void {
    this.confirmationService.confirm({
      message: 'Bạn muốn mở lại để tiếp tục nhập số liệu?',
      header: 'Mở lại khai báo',
      icon: 'pi pi-pencil',
      acceptLabel: 'Mở lại',
      rejectLabel: 'Hủy',
      accept: () => void this.setXacNhan(false),
    });
  }

  private async setXacNhan(value: boolean): Promise<void> {
    if (!this.selectedKyCode) return;
    this.saving = true;
    try {
      await this.tongHopApi.xacNhan(this.selectedKyCode, value);
      this.notification.show(
        'success',
        value ? 'Đã xác nhận hoàn tất khai báo.' : 'Đã mở lại để nhập tiếp.',
      );
      await this.loadMyTienDo();
    } catch {
      this.notification.show('error', 'Thao tác thất bại, vui lòng thử lại.');
    } finally {
      this.saving = false;
    }
  }
}
