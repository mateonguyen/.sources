import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DonViModeService } from '../../core/don-vi/don-vi-mode.service';

/**
 * Banner nhắc nhở trên các màn module nghiệp vụ khi đơn vị của user
 * đang ở chế độ nhập liệu TONG_HOP. Tự ẩn nếu là TU_NHAP.
 */
@Component({
  selector: 'app-tong-hop-mode-banner',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="thmb-banner" *ngIf="isTongHop">
      <i class="pi pi-info-circle"></i>
      <span>
        Đơn vị của bạn đang ở chế độ <strong>Tổng hợp</strong> — màn hình này
        chỉ hiển thị dữ liệu do chính đơn vị bạn nhập. Số liệu của các đơn vị
        cấp dưới sẽ được <strong>tự động gộp khi Nộp báo cáo</strong>; theo dõi
        chi tiết tại màn
        <a routerLink="/tien-do-tong-hop">Tiến độ tổng hợp</a>.
      </span>
    </div>
  `,
  styles: [
    `
      .thmb-banner {
        display: flex;
        align-items: flex-start;
        gap: 10px;
        padding: 10px 14px;
        margin-bottom: 14px;
        border-radius: var(--app-radius, 6px);
        background: #eef3f9;
        border: 1px solid #b8cfe0;
        color: #1f4f7c;
        font-size: 0.85rem;
        line-height: 1.5;
      }
      .thmb-banner .pi {
        font-size: 1rem;
        margin-top: 2px;
        flex-shrink: 0;
      }
      .thmb-banner a {
        color: #163b63;
        font-weight: 600;
        text-decoration: underline;
      }
    `,
  ],
})
export class TongHopModeBannerComponent {
  constructor(private readonly donViModeService: DonViModeService) {
    void this.donViModeService.ensureLoaded();
  }

  get isTongHop(): boolean {
    return this.donViModeService.isTongHop;
  }
}
