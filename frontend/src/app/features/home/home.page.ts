import { CommonModule, DecimalPipe } from '@angular/common';
import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { DropdownModule } from 'primeng/dropdown';
import { TableModule } from 'primeng/table';
import { AuthService } from '../../core/auth/auth.service';
import { DonViModeService } from '../../core/don-vi/don-vi-mode.service';
import { NotificationService } from '../../core/ui/notification.service';
import {
  KyBaoCaoApi,
  KyBaoCaoDto,
  KyBaoCaoTienDoDonViDto,
  KyBaoCaoTienDoDto,
} from '../ky-bao-cao/ky-bao-cao.api';
import {
  TienDoDonViDto,
  TongHopTienDoApi,
} from '../tong-hop-tien-do/tong-hop-tien-do.api';
import { EmptyStateComponent } from '../../shared/ui/empty-state.component';
import { LoadingOverlayComponent } from '../../shared/ui/loading-overlay.component';

interface QuickLink {
  label: string;
  route: string;
  icon: string;
  description: string;
  permission?: string;
  /** Ẩn khi đơn vị của user ở chế độ nhập liệu TONG_HOP. */
  hideWhenTongHop?: boolean;
}

@Component({
  selector: 'app-home-page',
  standalone: true,
  imports: [
    CommonModule,
    DecimalPipe,
    FormsModule,
    RouterLink,
    ButtonModule,
    DropdownModule,
    TableModule,
    EmptyStateComponent,
    LoadingOverlayComponent,
  ],
  template: `
    <div class="home-page">
      <!-- ── Welcome bar ── -->
      <div class="tc-welcome-bar">
        <div class="tc-welcome-bar__left">
          <div class="tc-welcome-bar__icon">
            <i class="pi pi-home"></i>
          </div>
          <div>
            <div class="tc-welcome-bar__title">Xin chào, {{ displayName }}</div>
            <div class="tc-welcome-bar__sub">
              Theo dõi kỳ báo cáo, tiến độ đơn vị và truy cập nhanh các phân hệ
              nhập liệu.
            </div>
          </div>
        </div>
        <a
          pButton
          routerLink="/ky-bao-cao"
          icon="pi pi-calendar"
          label="Quản lý kỳ"
          class="p-button-outlined p-button-sm tc-btn-ky"
        ></a>
      </div>

      <!-- ── Tiến độ card ── -->
      <div class="app-page-card tc-progress-card">
        <app-loading-overlay
          [active]="loading"
          label="Đang tải dashboard..."
        ></app-loading-overlay>

        <div class="tc-progress-card__header">
          <div>
            <h2 class="tc-card-title">
              {{ canViewKyProgress ? 'Tiến độ kỳ báo cáo' : 'Tiến độ của tôi' }}
            </h2>
            <p class="tc-card-sub">
              {{
                canViewKyProgress
                  ? 'Tổng hợp trạng thái nộp báo cáo theo kỳ và theo đơn vị trong phạm vi bạn được phép xem.'
                  : 'Theo dõi trạng thái khai báo của đơn vị và xác nhận hoàn tất.'
              }}
            </p>
          </div>
        </div>

        <!-- Kỳ bar -->
        <div class="tc-ky-bar">
          <span class="tc-ky-bar__label">Kỳ báo cáo</span>
          <div class="tc-ky-bar__sep"></div>
          <div class="tc-ky-bar__group">
            <p-dropdown
              [options]="kyOptions"
              optionLabel="label"
              optionValue="value"
              [ngModel]="selectedKyId"
              (ngModelChange)="onKyChange($event)"
              appendTo="body"
              placeholder="Chọn kỳ báo cáo"
              [style]="{ width: 'auto', minWidth: '280px', maxWidth: '380px' }"
            ></p-dropdown>
            <span class="tc-ky-badge" *ngIf="selectedKy?.trangThai === 2"
              >Đang mở</span
            >
            <span class="tc-ky-date" *ngIf="selectedKy?.ngayBatDau">
              {{ selectedKy!.ngayBatDau | date: 'dd/MM/yyyy' }}
              –
              {{ selectedKy!.ngayKetThuc | date: 'dd/MM/yyyy' }}
            </span>
          </div>
          <button
            pButton
            type="button"
            icon="pi pi-refresh"
            label="Làm mới"
            class="p-button-outlined p-button-sm tc-btn-refresh"
            (click)="onKyChange(selectedKyId)"
          ></button>
        </div>

        <ng-container
          *ngIf="canViewKyProgress && progress; else noDashboardData"
        >
          <!-- Stat row -->
          <div class="tc-stat-row">
            <div class="tc-stat-cell">
              <div class="tc-stat-lbl">Tổng đơn vị</div>
              <div class="tc-stat-val">{{ progress.tongDonVi }}</div>
            </div>
            <div class="tc-stat-cell">
              <div class="tc-stat-lbl">Đã nộp</div>
              <div class="tc-stat-val">{{ progress.soDonViDaNop }}</div>
            </div>
            <div class="tc-stat-cell">
              <div class="tc-stat-lbl">Đang bổ sung</div>
              <div class="tc-stat-val">{{ progress.soDonViDangBoSung }}</div>
            </div>
            <div class="tc-stat-cell">
              <div class="tc-stat-lbl">Đã xác nhận</div>
              <div class="tc-stat-val">{{ progress.soDonViDaXacNhan }}</div>
            </div>
            <div class="tc-stat-cell tc-stat-cell--alert">
              <div class="tc-stat-lbl tc-stat-lbl--alert">Chưa nhập</div>
              <div class="tc-stat-val tc-stat-val--alert">
                {{ progress.soDonViChuaNhap }}
              </div>
            </div>
          </div>

          <!-- Progress row -->
          <div class="tc-prog-row" *ngIf="progress.tongDonVi > 0">
            <span class="tc-prog-text">
              {{ progress.soDonViDaNop }} / {{ progress.tongDonVi }} đơn vị đã
              nộp
            </span>
            <div class="tc-prog-track">
              <div
                class="tc-prog-fill"
                [style.width.%]="submissionPercent"
              ></div>
            </div>
            <span class="tc-prog-pct"
              >{{ submissionPercent | number: '1.0-0' }}%</span
            >
          </div>

          <!-- Table -->
          <div class="tc-table-wrap">
            <p-table
              [value]="progress.donVis"
              styleClass="home-progress-table app-admin-table"
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
          </div>
        </ng-container>

        <ng-template #noDashboardData>
          <div
            class="tc-empty-wrap"
            *ngIf="canViewMyProgress && myTienDo; else noPermissionData"
          >
            <div
              class="tc-stat-row"
              style="grid-template-columns: repeat(3, 1fr); border-bottom: 0;"
            >
              <div class="tc-stat-cell">
                <div class="tc-stat-lbl">Đơn vị</div>
                <div
                  class="tc-stat-val"
                  style="font-size: 1rem; line-height: 1.3;"
                >
                  {{ myTienDo.tenDonVi }}
                </div>
              </div>
              <div class="tc-stat-cell">
                <div class="tc-stat-lbl">Đã xác nhận</div>
                <div
                  class="tc-stat-val"
                  [class.tc-stat-val--alert]="
                    myTienDo.daXacNhan && myTienDo.coThayDoiSauXacNhan
                  "
                >
                  {{
                    myTienDo.daXacNhan
                      ? myTienDo.coThayDoiSauXacNhan
                        ? 'Cần xác nhận lại'
                        : 'Có'
                      : 'Chưa'
                  }}
                </div>
              </div>
              <div class="tc-stat-cell">
                <div class="tc-stat-lbl">Tổng bản ghi</div>
                <div class="tc-stat-val">{{ myTotalRecords }}</div>
              </div>
            </div>
            <div class="mt-3" style="display:flex; justify-content:flex-end;">
              <button
                pButton
                type="button"
                [icon]="
                  myTienDo.daXacNhan && !myTienDo.coThayDoiSauXacNhan
                    ? 'pi pi-times'
                    : 'pi pi-check'
                "
                [label]="
                  myTienDo.daXacNhan
                    ? myTienDo.coThayDoiSauXacNhan
                      ? 'Xác nhận lại'
                      : 'Hủy xác nhận'
                    : 'Xác nhận hoàn tất'
                "
                class="p-button-sm"
                (click)="toggleMyXacNhan()"
              ></button>
            </div>
          </div>
          <ng-template #noPermissionData>
            <div class="tc-empty-wrap">
              <app-empty-state
                title="Chưa có dữ liệu tiến độ"
                message="Chọn kỳ báo cáo hoặc mở kỳ để hệ thống bắt đầu ghi nhận trạng thái các đơn vị."
                icon="pi-chart-line"
              ></app-empty-state>
            </div>
          </ng-template>
        </ng-template>
      </div>

      <!-- ── Quick access ── -->
      <div class="home-quick-links">
        <h2 class="home-section-title">Truy cập nhanh</h2>
        <div class="tc-qa-grid">
          <a
            *ngFor="let link of quickLinks"
            [routerLink]="link.route"
            class="tc-qa-card"
          >
            <span class="tc-qa-icon"><i [class]="link.icon"></i></span>
            <div class="tc-qa-body">
              <div class="tc-qa-name">{{ link.label }}</div>
              <div class="tc-qa-sub">{{ link.description }}</div>
            </div>
            <i class="pi pi-chevron-right tc-qa-arr"></i>
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
        gap: 1.5rem;
      }

      /* ── Welcome bar ── */
      .tc-welcome-bar {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: 16px;
        padding: 12px 18px;
        background: var(--app-surface);
        border: 1px solid var(--app-border);
        border-radius: 6px;
      }
      .tc-welcome-bar__left {
        display: flex;
        align-items: center;
        gap: 14px;
        min-width: 0;
      }
      .tc-welcome-bar__icon {
        width: 36px;
        height: 36px;
        border-radius: 6px;
        background: #eef3f9;
        color: var(--app-primary-500);
        display: flex;
        align-items: center;
        justify-content: center;
        flex-shrink: 0;
        font-size: 1rem;
      }
      .tc-welcome-bar__title {
        font-size: 1.08rem;
        font-weight: 700;
        color: var(--app-text-strong);
      }
      .tc-welcome-bar__sub {
        font-size: 0.8rem;
        color: var(--app-text-muted);
        margin-top: 1px;
      }
      .tc-btn-ky {
        flex-shrink: 0;
        white-space: nowrap;
      }
      .tc-btn-refresh {
        margin-left: auto;
        flex-shrink: 0;
        white-space: nowrap;
      }

      /* ── Tiến độ card ── */
      .tc-progress-card {
        position: relative;
        overflow: hidden;
      }
      .tc-progress-card__header {
        padding: 16px 20px 12px;
        border-bottom: 1px solid var(--app-border);
      }
      .tc-card-title {
        font-size: 0.95rem;
        font-weight: 700;
        color: var(--app-text-strong);
        text-transform: uppercase;
        letter-spacing: 0.04em;
        margin: 0 0 2px;
      }
      .tc-card-sub {
        font-size: 0.8rem;
        color: var(--app-text-muted);
        margin: 0;
      }

      /* ── Kỳ bar ── */
      .tc-ky-bar {
        display: flex;
        align-items: center;
        gap: 10px;
        padding: 9px 14px;
        background: #eef3f9;
        border-bottom: 1px solid #c4d8ea;
        flex-wrap: nowrap;
      }
      .tc-ky-bar__label {
        font-size: 0.63rem;
        font-weight: 700;
        letter-spacing: 0.08em;
        text-transform: uppercase;
        color: var(--app-primary-500);
        white-space: nowrap;
        flex-shrink: 0;
      }
      .tc-ky-bar__sep {
        width: 1px;
        height: 16px;
        background: #c4d8ea;
        flex-shrink: 0;
      }
      .tc-ky-bar__group {
        display: flex;
        align-items: center;
        gap: 8px;
        flex-shrink: 0;
      }
      :host ::ng-deep .tc-ky-bar .p-dropdown {
        border: 1.5px solid var(--app-primary-500) !important;
        border-radius: 6px !important;
        background: #fff;
        width: auto !important;
        min-width: 280px;
        max-width: 380px;
      }
      :host ::ng-deep .tc-ky-bar .p-dropdown .p-dropdown-label {
        font-weight: 600;
        font-size: 0.87rem;
        color: var(--app-primary-500);
        padding: 5px 10px;
      }
      :host ::ng-deep .tc-ky-bar .p-dropdown .p-dropdown-trigger {
        color: var(--app-primary-500);
      }
      .tc-ky-badge {
        display: inline-flex;
        align-items: center;
        gap: 4px;
        padding: 3px 8px;
        border-radius: 6px;
        border: 1px solid #5a8a44;
        background: #edf3e9;
        color: #3f5f2d;
        font-size: 0.72rem;
        font-weight: 600;
        white-space: nowrap;
      }
      .tc-ky-badge::before {
        content: '';
        width: 6px;
        height: 6px;
        border-radius: 50%;
        background: #5a8a44;
        display: block;
        flex-shrink: 0;
      }
      .tc-ky-date {
        font-size: 0.76rem;
        color: var(--app-text-muted);
        white-space: nowrap;
      }

      /* ── Stat row ── */
      .tc-stat-row {
        display: grid;
        grid-template-columns: repeat(5, 1fr);
        border-bottom: 1px solid var(--app-border);
      }
      .tc-stat-cell {
        padding: 14px 16px;
        border-right: 1px solid var(--app-border);
      }
      .tc-stat-cell:last-child {
        border-right: 0;
      }
      .tc-stat-cell--alert {
        background: #fef5f5;
      }
      .tc-stat-lbl {
        font-size: 0.62rem;
        font-weight: 700;
        letter-spacing: 0.07em;
        text-transform: uppercase;
        color: var(--app-text-muted);
        margin-bottom: 5px;
      }
      .tc-stat-lbl--alert {
        color: #8a1f2d;
      }
      .tc-stat-val {
        font-size: 1.65rem;
        font-weight: 700;
        color: var(--app-text-strong);
        line-height: 1;
      }
      .tc-stat-val--alert {
        color: #8a1f2d;
      }

      /* ── Progress row ── */
      .tc-prog-row {
        display: flex;
        align-items: center;
        gap: 10px;
        padding: 8px 14px;
        border-bottom: 1px solid var(--app-border);
      }
      .tc-prog-text {
        font-size: 0.76rem;
        color: var(--app-text-muted);
        white-space: nowrap;
      }
      .tc-prog-track {
        flex: 1;
        height: 6px;
        border-radius: 3px;
        background: rgba(120, 95, 55, 0.14);
        overflow: hidden;
      }
      .tc-prog-fill {
        height: 6px;
        border-radius: 3px;
        background: var(--app-primary-500);
        transition: width 0.4s ease;
      }
      .tc-prog-pct {
        font-size: 0.76rem;
        font-weight: 700;
        color: var(--app-primary-500);
        min-width: 30px;
        text-align: right;
      }

      /* ── Table ── */
      .tc-table-wrap {
        overflow-x: auto;
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
        border-radius: 6px;
        border: 1px solid transparent;
        font-size: 0.8125rem;
        font-weight: 600;
      }
      .tag-chua-nhap {
        background: #f3f5f7;
        color: #52606d;
        border-color: #d4dae0;
      }
      .tag-dang-nhap {
        background: #e8f2ff;
        color: #1d5fa8;
        border-color: #b8d4f0;
      }
      .tag-da-xac-nhan {
        background: #f4ecff;
        color: #6d3bb8;
        border-color: #d0b8f0;
      }
      .tag-dang-bo-sung {
        background: #fff4de;
        color: #9a5a00;
        border-color: #f0d8a0;
      }
      .tag-da-nop {
        background: #e8f8ef;
        color: #1f7a45;
        border-color: #b0dfc0;
      }
      .tc-empty-wrap {
        padding: 2rem 1.5rem;
      }

      /* ── Quick access ── */
      .home-section-title {
        font-size: 0.8125rem;
        font-weight: 700;
        text-transform: uppercase;
        letter-spacing: 0.05em;
        color: var(--app-text-muted);
        margin: 0 0 0.75rem;
      }
      .tc-qa-grid {
        display: grid;
        grid-template-columns: repeat(3, 1fr);
        gap: 8px;
      }
      .tc-qa-card {
        background: var(--app-surface);
        border: 1px solid var(--app-border);
        border-radius: 6px;
        padding: 12px 14px;
        display: flex;
        align-items: center;
        gap: 11px;
        text-decoration: none;
        color: var(--app-text);
        transition:
          background 120ms,
          border-color 120ms;
      }
      .tc-qa-card:hover {
        background: var(--app-surface-soft);
        border-color: #b8a48a;
      }
      .tc-qa-icon {
        width: 34px;
        height: 34px;
        border-radius: 6px;
        background: #eef3f9;
        color: var(--app-primary-500);
        display: flex;
        align-items: center;
        justify-content: center;
        flex-shrink: 0;
        font-size: 0.95rem;
      }
      .tc-qa-body {
        min-width: 0;
        flex: 1;
      }
      .tc-qa-name {
        font-size: 0.85rem;
        font-weight: 600;
        color: var(--app-text-strong);
      }
      .tc-qa-sub {
        font-size: 0.73rem;
        color: var(--app-text-muted);
        margin-top: 1px;
      }
      .tc-qa-arr {
        font-size: 0.72rem;
        color: #c8bfb2;
        flex-shrink: 0;
      }

      @media (max-width: 900px) {
        .tc-stat-row {
          grid-template-columns: repeat(3, 1fr);
        }
        .tc-qa-grid {
          grid-template-columns: repeat(2, 1fr);
        }
        .tc-welcome-bar {
          flex-direction: column;
          align-items: flex-start;
        }
      }
      @media (max-width: 600px) {
        .tc-stat-row {
          grid-template-columns: repeat(2, 1fr);
        }
        .tc-qa-grid {
          grid-template-columns: 1fr 1fr;
        }
      }
    `,
  ],
})
export class HomePage {
  selectedKyId: number | null = null;
  selectedKyCode: string | null = null;
  progress: KyBaoCaoTienDoDto | null = null;
  myTienDo: TienDoDonViDto | null = null;
  loading = false;

  private readonly allQuickLinks: QuickLink[] = [
    {
      label: 'Tiến độ của tôi',
      route: '/tong-hop-du-lieu',
      icon: 'pi pi-chart-line',
      description: 'Theo dõi và xác nhận trạng thái khai báo',
      permission: 'tong_hop_tien_do:xac_nhan',
    },
    {
      label: 'Tra cứu báo cáo',
      route: '/tra-cuu-bao-cao',
      icon: 'pi pi-search',
      description: 'Xem snapshot và báo cáo đã nộp',
      permission: 'snapshot:read',
    },
    {
      label: 'Người dùng',
      route: '/nguoi-dung',
      icon: 'pi pi-user',
      description: 'Quản lý tài khoản',
      permission: 'users:read',
    },
    {
      label: 'Đơn vị',
      route: '/don-vi',
      icon: 'pi pi-sitemap',
      description: 'Cây tổ chức',
      permission: 'don_vi:read',
    },
    {
      label: 'Yêu cầu bổ sung',
      route: '/yeu-cau-bo-sung',
      icon: 'pi pi-info-circle',
      description: 'Mở lại snapshot theo đơn vị',
      permission: 'yeu_cau_bo_sung:approve',
    },
    {
      label: 'Nhân lực CNTT',
      route: '/nhan-luc-cntt',
      icon: 'pi pi-users',
      description: 'Thực trạng nhân lực',
      permission: 'nhan_luc_cntt:read',
      hideWhenTongHop: true,
    },
    {
      label: 'Thiết bị CNTT',
      route: '/thiet-bi-cntt',
      icon: 'pi pi-desktop',
      description: 'Quản lý thiết bị',
      permission: 'thiet_bi_cntt:read',
      hideWhenTongHop: true,
    },
    {
      label: 'Hệ thống thông tin',
      route: '/he-thong-thong-tin',
      icon: 'pi pi-server',
      description: 'Các hệ thống đang vận hành',
      permission: 'he_thong_thong_tin:read',
      hideWhenTongHop: true,
    },
    {
      label: 'Kỳ báo cáo',
      route: '/ky-bao-cao',
      icon: 'pi pi-calendar',
      description: 'Chu kỳ ghi nhận',
      permission: 'ky_bao_cao:read',
    },
    {
      label: 'Mẫu báo cáo',
      route: '/mau-bao-cao',
      icon: 'pi pi-file',
      description: 'Quản lý template báo cáo',
      permission: 'mau_bao_cao:read',
    },
  ];

  private kyItems: KyBaoCaoDto[] = [];

  constructor(
    public readonly authService: AuthService,
    private readonly donViModeService: DonViModeService,
    private readonly kyBaoCaoApi: KyBaoCaoApi,
    private readonly tongHopTienDoApi: TongHopTienDoApi,
    private readonly notificationService: NotificationService,
  ) {
    void this.donViModeService.ensureLoaded();
    void this.load();
  }

  get canViewKyProgress(): boolean {
    return (
      !this.canViewMyProgress &&
      (this.authService.hasPermission('tong_hop_tien_do:read') ||
        this.authService.hasPermission('snapshot:read'))
    );
  }

  get canViewMyProgress(): boolean {
    return this.authService.hasPermission('tong_hop_tien_do:xac_nhan');
  }

  get quickLinks(): QuickLink[] {
    const isTongHop = this.donViModeService.isTongHop;
    return this.allQuickLinks.filter(
      (link) =>
        (!link.permission || this.authService.hasPermission(link.permission)) &&
        !(isTongHop && link.hideWhenTongHop),
    );
  }

  get myTotalRecords(): number {
    if (!this.myTienDo) {
      return 0;
    }

    return (
      this.myTienDo.soNhanLuc +
      this.myTienDo.soNangLucSo +
      this.myTienDo.soDaoTao +
      this.myTienDo.soDaoTaoHocVien +
      this.myTienDo.soHeThongThongTin +
      this.myTienDo.soHtttTieuChuan +
      this.myTienDo.soDuAn +
      this.myTienDo.soThietBi +
      this.myTienDo.soHaTangMang +
      this.myTienDo.soGiamSatNoc +
      this.myTienDo.soCameraQuanLy +
      this.myTienDo.soCameraThucTrang +
      this.myTienDo.soGiamSatSoc +
      this.myTienDo.soAtttVanHanh +
      this.myTienDo.soAtttDauTu +
      this.myTienDo.soAtttGiaiPhap +
      this.myTienDo.soVanBanQppl
    );
  }

  get displayName(): string {
    const profile = this.authService.profile();
    return profile?.hoTen || profile?.username || 'bạn';
  }

  get kyOptions(): Array<{ label: string; value: number }> {
    return this.kyItems.map((item) => ({
      label: item.tenKy || item.kyCode,
      value: item.id,
    }));
  }

  get selectedKy(): KyBaoCaoDto | null {
    if (!this.selectedKyId) return null;
    return this.kyItems.find((item) => item.id === this.selectedKyId) ?? null;
  }

  get submissionPercent(): number {
    if (!this.progress || this.progress.tongDonVi <= 0) return 0;
    return Math.round(
      (this.progress.soDonViDaNop / this.progress.tongDonVi) * 100,
    );
  }

  async load(): Promise<void> {
    this.loading = true;
    try {
      const canReadKy = this.authService.hasPermission('ky_bao_cao:read');

      if (canReadKy) {
        const [currentKy, allKyItems] = await Promise.all([
          this.kyBaoCaoApi.getCurrent().catch(() => null),
          this.kyBaoCaoApi.getAll().catch(() => [] as KyBaoCaoDto[]),
        ]);
        // Dashboard chi can chon/xem ky dang mo (trangThai === 2) - danh
        // sach day du (Chuan bi/Da dong/Khoa) de o trang "Ky bao cao" rieng.
        // Neu khong loc, dropdown se phinh to theo thoi gian voi ca ky da
        // dong/het han, gay kho chon nham lan.
        const openKyItems = allKyItems.filter((item) => item.trangThai === 2);
        this.kyItems = openKyItems;
        this.selectedKyId = currentKy?.id ?? openKyItems[0]?.id ?? null;
        this.selectedKyCode = this.selectedKy?.kyCode ?? null;
      } else {
        this.kyItems = [];
        this.selectedKyId = null;
        this.selectedKyCode = null;
      }

      if (this.canViewKyProgress && this.selectedKyId) {
        this.progress = await this.kyBaoCaoApi.getTienDo(this.selectedKyId);
      } else {
        this.progress = null;
      }

      if (this.canViewMyProgress && this.selectedKyCode) {
        this.myTienDo = await this.tongHopTienDoApi.getMyTienDo(
          this.selectedKyCode,
        );
      } else {
        this.myTienDo = null;
      }
    } finally {
      this.loading = false;
    }
  }

  async onKyChange(value: number | null | undefined): Promise<void> {
    if (!this.authService.hasPermission('ky_bao_cao:read')) {
      this.selectedKyId = null;
      this.selectedKyCode = null;
      this.progress = null;
      this.myTienDo = null;
      return;
    }

    this.selectedKyId = value ?? null;
    this.selectedKyCode = this.selectedKy?.kyCode ?? null;
    if (!this.selectedKyId) {
      this.progress = null;
      this.myTienDo = null;
      return;
    }
    this.loading = true;
    try {
      if (this.canViewKyProgress) {
        this.progress = await this.kyBaoCaoApi.getTienDo(this.selectedKyId);
      } else {
        this.progress = null;
      }

      if (this.canViewMyProgress && this.selectedKyCode) {
        this.myTienDo = await this.tongHopTienDoApi.getMyTienDo(
          this.selectedKyCode,
        );
      } else {
        this.myTienDo = null;
      }
    } finally {
      this.loading = false;
    }
  }

  async toggleMyXacNhan(): Promise<void> {
    if (!this.selectedKyCode || !this.myTienDo) {
      return;
    }

    this.loading = true;
    try {
      // Đang "cần xác nhận lại" (stale) → xác nhận lại (true); còn lại → toggle
      const nextValue = this.myTienDo.coThayDoiSauXacNhan
        ? true
        : !this.myTienDo.daXacNhan;
      await this.tongHopTienDoApi.xacNhan(this.selectedKyCode, nextValue);
      this.myTienDo = await this.tongHopTienDoApi.getMyTienDo(
        this.selectedKyCode,
      );
      this.notificationService.show(
        'success',
        this.myTienDo.daXacNhan
          ? 'Đã xác nhận hoàn tất.'
          : 'Đã hủy xác nhận — cấp trên sẽ thấy số liệu chưa chốt.',
      );
    } catch {
      this.notificationService.show(
        'error',
        'Không thể cập nhật trạng thái xác nhận.',
      );
    } finally {
      this.loading = false;
    }
  }

  kyStatusLabel(status: number): string {
    const map: Record<number, string> = {
      1: 'Chuẩn bị',
      2: 'Đang mở',
      3: 'Đã đóng',
      4: 'Khóa',
    };
    return map[status] ?? 'Không xác định';
  }

  progressStatusLabel(status: number): string {
    const map: Record<number, string> = {
      1: 'Chưa nhập',
      2: 'Đang nhập',
      3: 'Đã xác nhận',
      4: 'Đang bổ sung',
      5: 'Đã nộp',
    };
    return map[status] ?? 'Không xác định';
  }

  progressTagClass(status: number): string {
    const map: Record<number, string> = {
      1: 'dashboard-tag tag-chua-nhap',
      2: 'dashboard-tag tag-dang-nhap',
      3: 'dashboard-tag tag-da-xac-nhan',
      4: 'dashboard-tag tag-dang-bo-sung',
      5: 'dashboard-tag tag-da-nop',
    };
    return map[status] ?? 'dashboard-tag tag-chua-nhap';
  }

  snapshotStatusLabel(status?: number): string {
    const map: Record<number, string> = {
      1: 'Draft',
      2: 'Submitted',
      3: 'Locked',
      4: 'Superseded',
    };
    return status != null ? (map[status] ?? '-') : '-';
  }

  progressTimestamp(row: KyBaoCaoTienDoDonViDto): string {
    return (
      row.lockedAt || row.submittedAt || row.ngayMoLai || row.ngayXacNhan || '-'
    );
  }
}
