import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { DropdownModule } from 'primeng/dropdown';
import { TableModule } from 'primeng/table';
import { AuthService } from '../../core/auth/auth.service';
import {
  KyBaoCaoApi,
  KyBaoCaoDto,
  KyBaoCaoTienDoDonViDto,
  KyBaoCaoTienDoDto,
} from '../ky-bao-cao/ky-bao-cao.api';
import { EmptyStateComponent } from '../../shared/ui/empty-state.component';
import { LoadingOverlayComponent } from '../../shared/ui/loading-overlay.component';
import { SectionCardComponent } from '../../shared/ui/section-card.component';

interface QuickLink {
  label: string;
  route: string;
  icon: string;
  description: string;
}

@Component({
  selector: 'app-home-page',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    ButtonModule,
    DropdownModule,
    TableModule,
    SectionCardComponent,
    EmptyStateComponent,
    LoadingOverlayComponent,
  ],
  template: `
    <div class="home-page">
      <section class="home-hero">
        <div class="home-hero__text">
          <p class="home-hero__eyebrow">Dashboard báo cáo CNTT</p>
          <h1 class="home-hero__title">Xin chào, {{ displayName }}</h1>
          <p class="home-hero__sub">
            Theo dõi kỳ báo cáo, tiến độ đơn vị và truy cập nhanh các phân hệ
            nhập liệu.
          </p>
        </div>
        <div class="home-hero__actions">
          <a
            pButton
            routerLink="/ky-bao-cao"
            label="Quản lý kỳ"
            icon="pi pi-calendar"
            severity="secondary"
          ></a>
        </div>
      </section>

      <app-section-card
        title="Tiến độ kỳ báo cáo"
        subtitle="Tổng hợp trạng thái nộp báo cáo theo kỳ và theo đơn vị trong phạm vi bạn được phép xem."
        styleClass="home-dashboard-card"
      >
        <div class="dashboard-panel">
          <app-loading-overlay
            [active]="loading"
            label="Đang tải dashboard..."
          ></app-loading-overlay>

          <div class="dashboard-toolbar">
            <div class="dashboard-toolbar__field">
              <label for="homeKyBaoCao">Kỳ báo cáo</label>
              <p-dropdown
                inputId="homeKyBaoCao"
                [options]="kyOptions"
                optionLabel="label"
                optionValue="value"
                [ngModel]="selectedKyId"
                (ngModelChange)="onKyChange($event)"
                placeholder="Chọn kỳ báo cáo"
              ></p-dropdown>
            </div>

            <div class="dashboard-toolbar__meta" *ngIf="selectedKy">
              <span class="dashboard-chip">{{
                kyStatusLabel(selectedKy.trangThai)
              }}</span>
              <span *ngIf="selectedKy.ngayBatDau || selectedKy.ngayKetThuc">
                {{ formatKyDateRange(selectedKy) }}
              </span>
            </div>
          </div>

          <ng-container *ngIf="progress; else noDashboardData">
            <div class="dashboard-metrics">
              <article class="metric-card metric-card--neutral">
                <span class="metric-card__label">Tổng đơn vị</span>
                <strong>{{ progress.tongDonVi }}</strong>
              </article>
              <article class="metric-card metric-card--success">
                <span class="metric-card__label">Đã nộp</span>
                <strong>{{ progress.soDonViDaNop }}</strong>
              </article>
              <article class="metric-card metric-card--warning">
                <span class="metric-card__label">Đang bổ sung</span>
                <strong>{{ progress.soDonViDangBoSung }}</strong>
              </article>
              <article class="metric-card metric-card--info">
                <span class="metric-card__label">Đã xác nhận</span>
                <strong>{{ progress.soDonViDaXacNhan }}</strong>
              </article>
              <article class="metric-card metric-card--muted">
                <span class="metric-card__label">Chưa nhập</span>
                <strong>{{ progress.soDonViChuaNhap }}</strong>
              </article>
            </div>

            <div class="dashboard-progressbar" *ngIf="progress.tongDonVi > 0">
              <div
                class="dashboard-progressbar__value"
                [style.width.%]="submissionPercent"
              ></div>
            </div>
            <p
              class="dashboard-progressbar__caption"
              *ngIf="progress.tongDonVi > 0"
            >
              {{ submissionPercent | number: '1.0-0' }}% đơn vị đã nộp báo cáo
              cho {{ progress.kyCode }}.
            </p>

            <p-table
              [value]="progress.donVis"
              styleClass="home-progress-table"
              [tableStyle]="{ 'min-width': '52rem' }"
              [rows]="8"
            >
              <ng-template pTemplate="header">
                <tr>
                  <th>Đơn vị</th>
                  <th>Trạng thái đơn vị</th>
                  <th>Snapshot gần nhất</th>
                  <th>Phiên bản</th>
                  <th>Thời điểm gần nhất</th>
                </tr>
              </ng-template>
              <ng-template pTemplate="body" let-row>
                <tr>
                  <td>
                    <div class="progress-unit-cell">
                      <strong>{{ row.tenDonVi }}</strong>
                      <span>#{{ row.donViId }}</span>
                    </div>
                  </td>
                  <td>
                    <span
                      class="dashboard-tag"
                      [class]="progressTagClass(row.trangThaiDonVi)"
                    >
                      {{ progressStatusLabel(row.trangThaiDonVi) }}
                    </span>
                  </td>
                  <td>{{ snapshotStatusLabel(row.snapshotTrangThai) }}</td>
                  <td>{{ row.snapshotPhienBan || '-' }}</td>
                  <td>{{ progressTimestamp(row) }}</td>
                </tr>
              </ng-template>
            </p-table>
          </ng-container>

          <ng-template #noDashboardData>
            <app-empty-state
              title="Chưa có dữ liệu tiến độ"
              message="Chọn kỳ báo cáo hoặc mở kỳ để hệ thống bắt đầu ghi nhận trạng thái các đơn vị."
              icon="pi-chart-line"
            ></app-empty-state>
          </ng-template>
        </div>
      </app-section-card>

      <div class="home-quick-links">
        <h2 class="home-section-title">Truy cập nhanh</h2>
        <div class="home-quick-grid">
          <a
            *ngFor="let link of quickLinks"
            [routerLink]="link.route"
            class="quick-card"
          >
            <span class="quick-card__icon"><i [class]="link.icon"></i></span>
            <span class="quick-card__label">{{ link.label }}</span>
            <span class="quick-card__desc">{{ link.description }}</span>
          </a>
        </div>
      </div>
    </div>
  `,
  styles: [
    `
      .home-page {
        display: flex;
        flex-direction: column;
        gap: 2rem;
        padding: 0.25rem;
      }

      .home-hero {
        display: flex;
        justify-content: space-between;
        align-items: end;
        gap: 1.5rem;
        background: linear-gradient(
          135deg,
          var(--app-primary-50) 0%,
          #fff 100%
        );
        border: 1px solid var(--app-border);
        border-radius: 0.75rem;
        padding: 2rem 2.25rem;
      }

      .home-hero__eyebrow {
        margin: 0 0 0.35rem;
        font-size: 0.75rem;
        font-weight: 700;
        letter-spacing: 0.08em;
        text-transform: uppercase;
        color: var(--app-primary-600);
      }

      .home-hero__title {
        font-size: 1.5rem;
        font-weight: 700;
        color: var(--app-primary-600);
        margin: 0 0 0.4rem;
      }

      .home-hero__sub {
        color: var(--app-text-muted);
        font-size: 0.9375rem;
        margin: 0;
      }

      .home-hero__actions {
        display: flex;
        gap: 0.75rem;
        flex-wrap: wrap;
      }

      .dashboard-panel {
        position: relative;
        display: flex;
        flex-direction: column;
        gap: 1rem;
      }

      .dashboard-toolbar {
        display: flex;
        justify-content: space-between;
        gap: 1rem;
        align-items: end;
        flex-wrap: wrap;
      }

      .dashboard-toolbar__field {
        min-width: 18rem;
      }

      .dashboard-toolbar__field label {
        display: block;
        margin-bottom: 0.4rem;
        font-size: 0.8125rem;
        font-weight: 600;
        color: var(--app-text-muted);
      }

      .dashboard-toolbar__meta {
        display: flex;
        gap: 0.65rem;
        align-items: center;
        flex-wrap: wrap;
        color: var(--app-text-muted);
        font-size: 0.875rem;
      }

      .dashboard-chip {
        display: inline-flex;
        align-items: center;
        padding: 0.3rem 0.6rem;
        border-radius: 999px;
        background: var(--app-surface-soft);
        border: 1px solid var(--app-border);
        color: var(--app-text-strong);
        font-weight: 600;
      }

      .dashboard-metrics {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(160px, 1fr));
        gap: 0.85rem;
      }

      .metric-card {
        border: 1px solid var(--app-border);
        border-radius: 0.875rem;
        padding: 0.95rem 1rem;
        background: #fff;
        display: flex;
        flex-direction: column;
        gap: 0.35rem;
      }

      .metric-card strong {
        font-size: 1.5rem;
        line-height: 1;
        color: var(--app-text-strong);
      }

      .metric-card__label {
        font-size: 0.8125rem;
        color: var(--app-text-muted);
        text-transform: uppercase;
        letter-spacing: 0.04em;
      }

      .metric-card--success {
        background: linear-gradient(180deg, #effaf4 0%, #ffffff 100%);
      }

      .metric-card--warning {
        background: linear-gradient(180deg, #fff8ea 0%, #ffffff 100%);
      }

      .metric-card--info {
        background: linear-gradient(180deg, #eef6ff 0%, #ffffff 100%);
      }

      .metric-card--muted {
        background: linear-gradient(180deg, #f7f8fa 0%, #ffffff 100%);
      }

      .dashboard-progressbar {
        width: 100%;
        height: 0.8rem;
        border-radius: 999px;
        background: #edf1f5;
        overflow: hidden;
      }

      .dashboard-progressbar__value {
        height: 100%;
        border-radius: inherit;
        background: linear-gradient(90deg, #1f7a45 0%, #3fa96a 100%);
      }

      .dashboard-progressbar__caption {
        margin: -0.15rem 0 0;
        font-size: 0.875rem;
        color: var(--app-text-muted);
      }

      .progress-unit-cell {
        display: flex;
        flex-direction: column;
        gap: 0.2rem;
      }

      .progress-unit-cell strong {
        color: var(--app-text-strong);
      }

      .progress-unit-cell span {
        color: var(--app-text-muted);
        font-size: 0.8125rem;
      }

      .dashboard-tag {
        display: inline-flex;
        align-items: center;
        padding: 0.28rem 0.6rem;
        border-radius: 999px;
        border: 1px solid transparent;
        font-size: 0.8125rem;
        font-weight: 600;
      }

      .tag-chua-nhap {
        background: #f3f5f7;
        color: #52606d;
      }

      .tag-dang-nhap {
        background: #e8f2ff;
        color: #1d5fa8;
      }

      .tag-da-xac-nhan {
        background: #f4ecff;
        color: #6d3bb8;
      }

      .tag-dang-bo-sung {
        background: #fff4de;
        color: #9a5a00;
      }

      .tag-da-nop {
        background: #e8f8ef;
        color: #1f7a45;
      }

      .home-section-title {
        font-size: 0.8125rem;
        font-weight: 700;
        text-transform: uppercase;
        letter-spacing: 0.05em;
        color: var(--app-text-muted);
        margin: 0 0 0.875rem;
      }

      .home-quick-grid {
        display: grid;
        grid-template-columns: repeat(auto-fill, minmax(180px, 1fr));
        gap: 0.875rem;
      }

      .quick-card {
        display: flex;
        flex-direction: column;
        gap: 0.4rem;
        padding: 1.25rem 1rem;
        background: #fff;
        border: 1px solid var(--app-border);
        border-radius: 0.625rem;
        text-decoration: none;
        color: var(--app-text);
        transition: all 150ms ease;
        cursor: pointer;
      }

      .quick-card:hover {
        border-color: var(--app-primary-500);
        box-shadow: 0 2px 8px rgba(42, 90, 138, 0.12);
        transform: translateY(-1px);
      }

      .quick-card__icon {
        font-size: 1.5rem;
        color: var(--app-primary-500);
        line-height: 1;
      }

      .quick-card__label {
        font-size: 0.9375rem;
        font-weight: 600;
        color: var(--app-text-strong);
        line-height: 1.3;
      }

      .quick-card__desc {
        font-size: 0.8125rem;
        color: var(--app-text-muted);
        line-height: 1.4;
      }

      @media (max-width: 900px) {
        .home-hero {
          flex-direction: column;
          align-items: stretch;
        }

        .home-hero__actions {
          width: 100%;
        }
      }
    `,
  ],
})
export class HomePage {
  currentKy: KyBaoCaoDto | null = null;
  selectedKyId: number | null = null;
  progress: KyBaoCaoTienDoDto | null = null;
  loading = false;

  readonly quickLinks: QuickLink[] = [
    {
      label: 'Người dùng',
      route: '/nguoi-dung',
      icon: 'pi pi-user',
      description: 'Quản lý tài khoản',
    },
    {
      label: 'Đơn vị',
      route: '/don-vi',
      icon: 'pi pi-sitemap',
      description: 'Cây tổ chức',
    },
    {
      label: 'Snapshot',
      route: '/snapshot',
      icon: 'pi pi-briefcase',
      description: 'Báo cáo tổng hợp',
    },
    {
      label: 'Yêu cầu bổ sung',
      route: '/yeu-cau-bo-sung',
      icon: 'pi pi-exclamation-circle',
      description: 'Mở lại snapshot theo đơn vị',
    },
    {
      label: 'Nhân lực CNTT',
      route: '/nhan-luc-cntt',
      icon: 'pi pi-users',
      description: 'Thực trạng nhân lực',
    },
    {
      label: 'Thiết bị CNTT',
      route: '/thiet-bi-cntt',
      icon: 'pi pi-desktop',
      description: 'Quản lý thiết bị',
    },
    {
      label: 'Hệ thống thông tin',
      route: '/he-thong-thong-tin',
      icon: 'pi pi-server',
      description: 'Các hệ thống đang vận hành',
    },
    {
      label: 'Kỳ báo cáo',
      route: '/ky-bao-cao',
      icon: 'pi pi-calendar',
      description: 'Chu kỳ ghi nhận',
    },
    {
      label: 'Mẫu báo cáo',
      route: '/mau-bao-cao',
      icon: 'pi pi-file-edit',
      description: 'Quản lý template báo cáo',
    },
  ];

  constructor(
    public readonly authService: AuthService,
    private readonly kyBaoCaoApi: KyBaoCaoApi,
  ) {
    void this.load();
  }

  get displayName(): string {
    const profile = this.authService.profile();
    return profile?.hoTen || profile?.username || 'bạn';
  }

  get kyOptions(): Array<{ label: string; value: number }> {
    return this.kyItems.map((item) => ({
      label: `${item.kyCode} · ${this.kyStatusLabel(item.trangThai)}`,
      value: item.id,
    }));
  }

  get selectedKy(): KyBaoCaoDto | null {
    if (!this.selectedKyId) {
      return null;
    }

    return this.kyItems.find((item) => item.id === this.selectedKyId) ?? null;
  }

  get submissionPercent(): number {
    if (!this.progress || this.progress.tongDonVi <= 0) {
      return 0;
    }

    return (this.progress.soDonViDaNop / this.progress.tongDonVi) * 100;
  }

  private kyItems: KyBaoCaoDto[] = [];

  async load(): Promise<void> {
    this.loading = true;
    try {
      const [currentKy, kyItems] = await Promise.all([
        this.kyBaoCaoApi.getCurrent().catch(() => null),
        this.kyBaoCaoApi.getAll(),
      ]);

      this.currentKy = currentKy;
      this.kyItems = kyItems;
      this.selectedKyId = currentKy?.id ?? kyItems[0]?.id ?? null;

      if (this.selectedKyId) {
        this.progress = await this.kyBaoCaoApi.getTienDo(this.selectedKyId);
      } else {
        this.progress = null;
      }
    } finally {
      this.loading = false;
    }
  }

  async onKyChange(value: number | null | undefined): Promise<void> {
    const nextValue = value ?? null;
    this.selectedKyId = nextValue;
    if (!nextValue) {
      this.progress = null;
      return;
    }

    this.loading = true;
    try {
      this.progress = await this.kyBaoCaoApi.getTienDo(nextValue);
    } finally {
      this.loading = false;
    }
  }

  kyStatusLabel(status: number): string {
    switch (status) {
      case 1:
        return 'Chuẩn bị';
      case 2:
        return 'Đang mở';
      case 3:
        return 'Đã đóng';
      case 4:
        return 'Khóa';
      default:
        return 'Không xác định';
    }
  }

  progressStatusLabel(status: number): string {
    switch (status) {
      case 1:
        return 'Chưa nhập';
      case 2:
        return 'Đang nhập';
      case 3:
        return 'Đã xác nhận';
      case 4:
        return 'Đang bổ sung';
      case 5:
        return 'Đã nộp';
      default:
        return 'Không xác định';
    }
  }

  progressTagClass(status: number): string {
    switch (status) {
      case 1:
        return 'tag-chua-nhap';
      case 2:
        return 'tag-dang-nhap';
      case 3:
        return 'tag-da-xac-nhan';
      case 4:
        return 'tag-dang-bo-sung';
      case 5:
        return 'tag-da-nop';
      default:
        return 'tag-chua-nhap';
    }
  }

  snapshotStatusLabel(status?: number): string {
    switch (status) {
      case 1:
        return 'Draft';
      case 2:
        return 'Submitted';
      case 3:
        return 'Locked';
      case 4:
        return 'Superseded';
      default:
        return '-';
    }
  }

  progressTimestamp(row: KyBaoCaoTienDoDonViDto): string {
    return (
      row.lockedAt || row.submittedAt || row.ngayMoLai || row.ngayXacNhan || '-'
    );
  }

  formatKyDateRange(item: KyBaoCaoDto): string {
    if (!item.ngayBatDau && !item.ngayKetThuc) {
      return 'Chưa cấu hình thời gian';
    }

    if (item.ngayBatDau && item.ngayKetThuc) {
      return `${item.ngayBatDau} đến ${item.ngayKetThuc}`;
    }

    return item.ngayBatDau || item.ngayKetThuc || '';
  }
}
