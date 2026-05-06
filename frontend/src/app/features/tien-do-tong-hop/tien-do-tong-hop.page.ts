import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DropdownModule } from 'primeng/dropdown';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { KyBaoCaoApi, KyBaoCaoDto } from '../ky-bao-cao/ky-bao-cao.api';
import {
  TienDoDonViDto,
  TongHopTienDoApi,
} from '../tong-hop-tien-do/tong-hop-tien-do.api';
import { EmptyStateComponent } from '../../shared/ui/empty-state.component';
import { LoadingOverlayComponent } from '../../shared/ui/loading-overlay.component';
import { SectionCardComponent } from '../../shared/ui/section-card.component';
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
    DropdownModule,
    TableModule,
    TagModule,
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

  constructor(
    private readonly kyBaoCaoApi: KyBaoCaoApi,
    private readonly tongHopApi: TongHopTienDoApi,
    private readonly notification: NotificationService,
  ) {}

  ngOnInit(): void {
    void this.loadKy();
  }

  get kyOptions(): { label: string; value: string }[] {
    return this.allKy.map((k) => ({ label: k.kyCode, value: k.kyCode }));
  }

  get soXacNhan(): number {
    return this.rows.filter((r) => r.daXacNhan).length;
  }

  async loadKy(): Promise<void> {
    this.loading = true;
    try {
      this.allKy = await this.kyBaoCaoApi.getAll();
      if (this.allKy.length > 0) {
        this.selectedKyCode = this.allKy[0].kyCode;
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
