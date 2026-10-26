import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';
import { Router } from '@angular/router';

interface PageHeaderPresentation {
  eyebrow: string;
  icon: string;
}

const PAGE_HEADER_PRESENTATION: Readonly<Record<string, PageHeaderPresentation>> = {
  '/nguoi-dung': { eyebrow: 'QUẢN TRỊ HỆ THỐNG', icon: 'pi pi-user' },
  '/vai-tro': { eyebrow: 'QUẢN TRỊ HỆ THỐNG', icon: 'pi pi-shield' },
  '/quyen': { eyebrow: 'QUẢN TRỊ HỆ THỐNG', icon: 'pi pi-key' },
  '/phan-quyen': { eyebrow: 'QUẢN TRỊ HỆ THỐNG', icon: 'pi pi-link' },
  '/don-vi': { eyebrow: 'QUẢN TRỊ HỆ THỐNG', icon: 'pi pi-sitemap' },
  '/codes': { eyebrow: 'QUẢN TRỊ HỆ THỐNG', icon: 'pi pi-list' },
  '/ref-loai-thiet-bi': { eyebrow: 'QUẢN TRỊ HỆ THỐNG', icon: 'pi pi-desktop' },
  '/tong-hop-du-lieu': { eyebrow: 'BÁO CÁO & TỔNG HỢP', icon: 'pi pi-check-circle' },
  '/nop-bao-cao': { eyebrow: 'BÁO CÁO & TỔNG HỢP', icon: 'pi pi-send' },
  '/yeu-cau-bo-sung': { eyebrow: 'BÁO CÁO & TỔNG HỢP', icon: 'pi pi-exclamation-circle' },
  '/tien-do-nop': { eyebrow: 'BÁO CÁO & TỔNG HỢP', icon: 'pi pi-list-check' },
  '/ky-bao-cao': { eyebrow: 'BÁO CÁO & TỔNG HỢP', icon: 'pi pi-calendar' },
  '/mau-bao-cao': { eyebrow: 'BÁO CÁO & TỔNG HỢP', icon: 'pi pi-file' },
  '/tra-cuu-bao-cao': { eyebrow: 'BÁO CÁO & TỔNG HỢP', icon: 'pi pi-search' },
  '/so-sanh-bao-cao': { eyebrow: 'BÁO CÁO & TỔNG HỢP', icon: 'pi pi-chart-line' },
  '/nhan-luc-cntt': { eyebrow: 'NHÂN LỰC CNTT', icon: 'pi pi-users' },
  '/dao-tao-boi-duong': { eyebrow: 'QUẢN LÝ ĐÀO TẠO', icon: 'pi pi-book' },
  '/dao-tao-hoc-vien': { eyebrow: 'QUẢN LÝ ĐÀO TẠO', icon: 'pi pi-graduation-cap' },
  '/nang-luc-so': { eyebrow: 'ĐÁNH GIÁ NĂNG LỰC SỐ', icon: 'pi pi-chart-bar' },
  '/thiet-bi-cntt': { eyebrow: 'HẠ TẦNG & HỆ THỐNG', icon: 'pi pi-desktop' },
  '/he-thong-thong-tin': { eyebrow: 'HẠ TẦNG & HỆ THỐNG', icon: 'pi pi-server' },
  '/ha-tang-mang': { eyebrow: 'HẠ TẦNG & HỆ THỐNG', icon: 'pi pi-wifi' },
  '/giam-sat-noc': { eyebrow: 'HẠ TẦNG & HỆ THỐNG', icon: 'pi pi-server' },
  '/camera-quan-ly': { eyebrow: 'HẠ TẦNG & HỆ THỐNG', icon: 'pi pi-video' },
  '/camera-thuc-trang': { eyebrow: 'HẠ TẦNG & HỆ THỐNG', icon: 'pi pi-map-marker' },
  '/du-an-cntt': { eyebrow: 'HẠ TẦNG & HỆ THỐNG', icon: 'pi pi-briefcase' },
  '/van-ban-qppl': { eyebrow: 'VĂN BẢN & QUẢN LÝ', icon: 'pi pi-book' },
  '/giam-sat-soc': { eyebrow: 'AN TOÀN THÔNG TIN', icon: 'pi pi-shield' },
  '/attt-httt-van-hanh': { eyebrow: 'AN TOÀN THÔNG TIN', icon: 'pi pi-lock' },
  '/attt-httt-dau-tu': { eyebrow: 'AN TOÀN THÔNG TIN', icon: 'pi pi-wallet' },
  '/giai-phap-attt': { eyebrow: 'AN TOÀN THÔNG TIN', icon: 'pi pi-shield' },
  '/tai-khoan/thong-tin': { eyebrow: 'TÀI KHOẢN CÁ NHÂN', icon: 'pi pi-id-card' },
  '/tai-khoan/doi-mat-khau': { eyebrow: 'TÀI KHOẢN CÁ NHÂN', icon: 'pi pi-lock' },
  '/tai-khoan/cau-hinh': { eyebrow: 'TÀI KHOẢN CÁ NHÂN', icon: 'pi pi-cog' },
  '/tro-giup': { eyebrow: 'HỖ TRỢ SỬ DỤNG', icon: 'pi pi-question-circle' },
};

@Component({
  selector: 'app-page-header',
  standalone: true,
  imports: [CommonModule],
  template: `
    <header class="app-page-header" [class.compact]="compact">
      <div class="app-page-header-main">
        <span class="app-page-header-icon" aria-hidden="true">
          <i [class]="resolvedIcon"></i>
        </span>
        <div class="app-page-header-content">
          <span class="app-page-header-eyebrow">{{ resolvedEyebrow }}</span>
          <h1 class="app-page-title">{{ title }}</h1>
          <p class="app-page-subtitle" *ngIf="subtitle">{{ subtitle }}</p>
        </div>
      </div>
      <div class="app-page-header-actions">
        <ng-content></ng-content>
      </div>
    </header>
  `,
  styles: [
    `
      :host {
        display: block;
        width: 100%;
      }

      .app-page-header {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: 1rem;
        flex-wrap: wrap;
        min-height: 3.4rem;
      }

      .app-page-header.compact {
        gap: 0.65rem;
      }

      .app-page-header-main {
        display: flex;
        align-items: center;
        gap: 0.85rem;
        min-width: 0;
      }

      .app-page-header-icon {
        display: inline-flex;
        flex: 0 0 2.8rem;
        align-items: center;
        justify-content: center;
        width: 2.8rem;
        height: 2.8rem;
        border: 1px solid color-mix(in srgb, var(--app-primary-500) 14%, white 86%);
        border-radius: 10px;
        background: color-mix(in srgb, var(--app-primary-500) 8%, white 92%);
        color: var(--app-primary-500);
        box-shadow: 0 2px 8px rgba(30, 58, 95, 0.08);
      }

      .app-page-header-icon i {
        font-size: 1.15rem;
      }

      .app-page-header-content {
        min-width: 0;
      }

      .app-page-header-eyebrow {
        display: block;
        margin-bottom: 0.12rem;
        color: #8a5b16;
        font-size: 0.68rem;
        font-weight: 750;
        letter-spacing: 0.09em;
        line-height: 1.2;
        text-transform: uppercase;
      }

      .app-page-header .app-page-title {
        margin: 0;
        color: var(--app-text-strong);
        font-size: 1.42rem;
        font-weight: 750;
        letter-spacing: 0;
        line-height: 1.18;
        text-transform: none;
      }

      .app-page-header .app-page-subtitle {
        margin: 0.2rem 0 0;
        color: var(--app-text-muted);
        font-size: 0.86rem;
        line-height: 1.35;
      }

      .app-page-header-actions {
        display: flex;
        align-items: center;
        gap: 0.6rem;
        flex-wrap: wrap;
        margin-left: auto;
      }

      @media (max-width: 720px) {
        .app-page-header {
          align-items: flex-start;
        }

        .app-page-header-actions {
          width: 100%;
          margin-left: 3.65rem;
        }

        .app-page-header .app-page-title {
          font-size: 1.25rem;
        }
      }
    `,
  ],
})
export class PageHeaderComponent {
  @Input({ required: true }) title = '';
  @Input() subtitle = '';
  @Input() eyebrow = '';
  @Input() icon = '';
  @Input() compact = false;

  constructor(private readonly router: Router) {}

  get resolvedEyebrow(): string {
    return this.eyebrow || this.presentation.eyebrow;
  }

  get resolvedIcon(): string {
    return this.icon || this.presentation.icon;
  }

  private get presentation(): PageHeaderPresentation {
    const path = this.normalizePath(this.router.url);
    const matchedRoute = Object.keys(PAGE_HEADER_PRESENTATION)
      .sort((a, b) => b.length - a.length)
      .find((route) => path === route || path.startsWith(`${route}/`));

    return (
      (matchedRoute ? PAGE_HEADER_PRESENTATION[matchedRoute] : null) ?? {
        eyebrow: 'QUẢN LÝ NGHIỆP VỤ',
        icon: 'pi pi-window-maximize',
      }
    );
  }

  private normalizePath(url: string): string {
    const path = (url || '/').split(/[?#]/, 1)[0];
    return `/${path.replace(/^\/+|\/+$/g, '')}`;
  }
}
