import { CommonModule, DecimalPipe } from '@angular/common';
import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { DropdownModule } from 'primeng/dropdown';
import { TableModule } from 'primeng/table';
import { AuthService } from '../../core/auth/auth.service';
import { getRoutePermission } from '../../core/auth/route-permissions';
import { DonViModeService } from '../../core/don-vi/don-vi-mode.service';
import { DEFAULT_QUICK_ROUTES, loadQuickLinkRoutes, quickLinkStorageKey } from '../../core/navigation/quick-links';
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
import {
  DonViDto,
  IdentityAdminApi,
  RoleDto,
  UserDto,
} from '../identity-admin/identity-admin.api';
import { MauBaoCaoApi, MauBaoCaoDto } from '../mau-bao-cao/mau-bao-cao.api';
import { EmptyStateComponent } from '../../shared/ui/empty-state.component';
import { LoadingOverlayComponent } from '../../shared/ui/loading-overlay.component';

interface AdminStats {
  totalUsers: number;
  activeUsers: number;
  totalDonVi: number;
  totalRoles: number;
  totalMauBaoCao: number;
}

interface QuickLink {
  label: string;
  route: string;
  icon: string;
  description: string;
  permission?: string;
  /** Ẩn khi đơn vị của user ở chế độ nhập liệu TONG_HOP. */
  hideWhenTongHop?: boolean;
}

const EXTRA_QUICK_LINKS: QuickLink[] = [
  { label: 'Vai trò', route: '/vai-tro', icon: 'pi pi-shield', description: 'Quản lý vai trò' },
  { label: 'Quyền', route: '/quyen', icon: 'pi pi-key', description: 'Danh sách quyền' },
  { label: 'Phân quyền', route: '/phan-quyen', icon: 'pi pi-link', description: 'Gán quyền truy cập' },
  { label: 'Danh mục chung', route: '/codes', icon: 'pi pi-list', description: 'Quản lý danh mục' },
  { label: 'Loại thiết bị CNTT', route: '/ref-loai-thiet-bi', icon: 'pi pi-desktop', description: 'Danh mục loại thiết bị' },
  { label: 'Tiến độ nộp', route: '/tien-do-nop', icon: 'pi pi-chart-bar', description: 'Theo dõi báo cáo' },
  { label: 'Nộp báo cáo', route: '/nop-bao-cao', icon: 'pi pi-upload', description: 'Gửi báo cáo' },
  { label: 'So sánh báo cáo', route: '/so-sanh-bao-cao', icon: 'pi pi-chart-line', description: 'Theo dõi biến động' },
  { label: 'Đào tạo bồi dưỡng', route: '/dao-tao-boi-duong', icon: 'pi pi-book', description: 'Dữ liệu đào tạo', hideWhenTongHop: true },
  { label: 'Đào tạo học viện', route: '/dao-tao-hoc-vien', icon: 'pi pi-graduation-cap', description: 'Dữ liệu học viện', hideWhenTongHop: true },
  { label: 'Năng lực số', route: '/nang-luc-so', icon: 'pi pi-chart-bar', description: 'Dữ liệu năng lực số', hideWhenTongHop: true },
  { label: 'Hạ tầng mạng', route: '/ha-tang-mang', icon: 'pi pi-sitemap', description: 'Dữ liệu hạ tầng', hideWhenTongHop: true },
  { label: 'Giám sát NOC', route: '/giam-sat-noc', icon: 'pi pi-eye', description: 'Dữ liệu giám sát', hideWhenTongHop: true },
  { label: 'Giám sát SOC', route: '/giam-sat-soc', icon: 'pi pi-shield', description: 'Dữ liệu giám sát', hideWhenTongHop: true },
  { label: 'Camera quản lý', route: '/camera-quan-ly', icon: 'pi pi-video', description: 'Quản lý camera', hideWhenTongHop: true },
  { label: 'Camera thực trạng', route: '/camera-thuc-trang', icon: 'pi pi-camera', description: 'Thực trạng camera', hideWhenTongHop: true },
  { label: 'Dự án CNTT', route: '/du-an-cntt', icon: 'pi pi-briefcase', description: 'Dữ liệu dự án', hideWhenTongHop: true },
  { label: 'Văn bản QPPL', route: '/van-ban-qppl', icon: 'pi pi-file', description: 'Dữ liệu văn bản', hideWhenTongHop: true },
  { label: 'ATTT vận hành', route: '/attt-httt-van-hanh', icon: 'pi pi-lock', description: 'An toàn vận hành', hideWhenTongHop: true },
  { label: 'ATTT đầu tư', route: '/attt-httt-dau-tu', icon: 'pi pi-lock', description: 'An toàn đầu tư', hideWhenTongHop: true },
  { label: 'Giải pháp ATTT', route: '/giai-phap-attt', icon: 'pi pi-shield', description: 'Giải pháp an toàn', hideWhenTongHop: true },
];

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
          <div class="tc-welcome-bar__content">
            <div class="tc-welcome-bar__eyebrow">Tổng quan hệ thống</div>
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
          *ngIf="canManageKy"
        ></a>
      </div>

      <!-- ── ADMIN: Tổng quan hệ thống ── -->
      <div class="app-page-card tc-progress-card" *ngIf="dashboardVariant === 'ADMIN'">
        <app-loading-overlay
          [active]="loadingAdminStats"
          label="Đang tải tổng quan hệ thống..."
        ></app-loading-overlay>

        <div class="tc-progress-card__header">
          <span class="tc-progress-card__icon" aria-hidden="true">
            <i class="pi pi-shield"></i>
          </span>
          <div class="tc-progress-card__heading">
            <h2 class="tc-card-title">Tổng quan hệ thống</h2>
            <p class="tc-card-sub">
              Số liệu quản trị: người dùng, đơn vị, vai trò và mẫu báo cáo trong hệ thống.
            </p>
          </div>
        </div>

        <div class="tc-stat-row" *ngIf="adminStats">
          <div class="tc-stat-cell">
            <span class="tc-stat-icon tc-stat-icon--all"><i class="pi pi-users"></i></span>
            <div class="tc-stat-lbl">Người dùng</div>
            <div class="tc-stat-val">{{ adminStats.totalUsers }}</div>
          </div>
          <div class="tc-stat-cell">
            <span class="tc-stat-icon tc-stat-icon--submitted"><i class="pi pi-user-plus"></i></span>
            <div class="tc-stat-lbl">Đang hoạt động</div>
            <div class="tc-stat-val">{{ adminStats.activeUsers }}</div>
          </div>
          <div class="tc-stat-cell">
            <span class="tc-stat-icon tc-stat-icon--confirmed"><i class="pi pi-sitemap"></i></span>
            <div class="tc-stat-lbl">Đơn vị</div>
            <div class="tc-stat-val">{{ adminStats.totalDonVi }}</div>
          </div>
          <div class="tc-stat-cell">
            <span class="tc-stat-icon tc-stat-icon--supplement"><i class="pi pi-shield"></i></span>
            <div class="tc-stat-lbl">Vai trò</div>
            <div class="tc-stat-val">{{ adminStats.totalRoles }}</div>
          </div>
          <div class="tc-stat-cell">
            <span class="tc-stat-icon tc-stat-icon--all"><i class="pi pi-file"></i></span>
            <div class="tc-stat-lbl">Mẫu báo cáo</div>
            <div class="tc-stat-val">{{ adminStats.totalMauBaoCao }}</div>
          </div>
        </div>
        <div class="tc-empty-wrap" *ngIf="!adminStats && !loadingAdminStats">
          <app-empty-state
            title="Chưa có dữ liệu tổng quan"
            message="Không thể tải số liệu quản trị hệ thống."
            icon="pi-shield"
          ></app-empty-state>
        </div>
      </div>

      <!-- ── UNIT (Cấp tỉnh / Cấp xã): dữ liệu đơn vị mình + cấp dưới ── -->
      <div class="app-page-card tc-progress-card" *ngIf="dashboardVariant === 'UNIT'">
        <app-loading-overlay
          [active]="loading"
          label="Đang tải dashboard..."
        ></app-loading-overlay>

        <div class="tc-progress-card__header">
          <span class="tc-progress-card__icon" aria-hidden="true">
            <i class="pi pi-building"></i>
          </span>
          <div class="tc-progress-card__heading">
            <h2 class="tc-card-title">Dữ liệu đơn vị của tôi</h2>
            <p class="tc-card-sub">
              {{
                donViModeService.isTongHop
                  ? 'Tổng hợp dữ liệu từ các đơn vị cấp dưới trực thuộc.'
                  : 'Tổng hợp dữ liệu các module nghiệp vụ của đơn vị mình.'
              }}
            </p>
          </div>
          <span class="tc-mode-badge" [class.tc-mode-badge--tonghop]="donViModeService.isTongHop">
            <i class="pi" [ngClass]="donViModeService.isTongHop ? 'pi-sitemap' : 'pi-pencil'"></i>
            {{ donViModeService.isTongHop ? 'Chế độ: Tổng hợp' : 'Chế độ: Tự nhập' }}
          </span>
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

        <ng-container *ngIf="!donViModeService.isTongHop; else subUnitTable">
          <div class="tc-stat-row tc-stat-row--personal" *ngIf="myTienDo">
            <div class="tc-stat-cell">
              <span class="tc-stat-icon tc-stat-icon--all"><i class="pi pi-building"></i></span>
              <div class="tc-stat-lbl">Đơn vị</div>
              <div class="tc-stat-val tc-stat-val--unit">{{ myTienDo.tenDonVi }}</div>
            </div>
            <div class="tc-stat-cell" *ngIf="myTienDo.parentIsTongHop">
              <span class="tc-stat-icon tc-stat-icon--confirmed"><i class="pi pi-verified"></i></span>
              <div class="tc-stat-lbl">Đã xác nhận với cấp trên</div>
              <div
                class="tc-stat-val"
                [class.tc-stat-val--alert]="myTienDo.daXacNhan && myTienDo.coThayDoiSauXacNhan"
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
              <span class="tc-stat-icon tc-stat-icon--submitted"><i class="pi pi-list-check"></i></span>
              <div class="tc-stat-lbl">Tổng bản ghi</div>
              <div class="tc-stat-val">{{ myTotalRecords }}</div>
            </div>
          </div>

          <!-- Module breakdown - luon hien dang the thong ke (giong man Hien trang), ke ca khi chua co du lieu -->
          <div class="tc-module-grid">
            <div class="tc-module-card" *ngFor="let m of unitModuleBreakdown" [class.tc-module-card--empty]="m.count === 0">
              <div class="tc-module-card__title">
                <span class="tc-module-card__icon"><i class="pi pi-chart-bar"></i></span>
                <span>{{ m.label }}</span>
              </div>
              <div class="tc-module-card__val">{{ m.count }}</div>
              <small>bản ghi</small>
            </div>
          </div>

          <div class="tc-personal-actions" *ngIf="myTienDo && myTienDo.parentIsTongHop">
            <button
              pButton
              type="button"
              [icon]="myTienDo.daXacNhan && !myTienDo.coThayDoiSauXacNhan ? 'pi pi-times' : 'pi pi-check'"
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
        </ng-container>

        <!-- Cấp dưới (chỉ hiện khi đơn vị có dữ liệu tổng hợp từ cấp dưới) -->
        <ng-template #subUnitTable>
          <div class="tc-subunit-stat-row">
            <div class="tc-subunit-stat">
              <span class="tc-subunit-stat__val">{{ subUnits.length }}</span>
              <span class="tc-subunit-stat__lbl">Đơn vị cấp dưới</span>
            </div>
            <div class="tc-subunit-stat">
              <span class="tc-subunit-stat__val">{{ subUnitStats.confirmed }}</span>
              <span class="tc-subunit-stat__lbl">Đã xác nhận</span>
            </div>
            <div class="tc-subunit-stat tc-subunit-stat--alert" *ngIf="subUnitStats.unconfirmed">
              <span class="tc-subunit-stat__val">{{ subUnitStats.unconfirmed }}</span>
              <span class="tc-subunit-stat__lbl">Chưa xác nhận</span>
            </div>
            <div class="tc-subunit-stat">
              <span class="tc-subunit-stat__val">{{ subUnitRecordTotal | number }}</span>
              <span class="tc-subunit-stat__lbl">Tổng bản ghi</span>
            </div>
            <a routerLink="/tong-hop-du-lieu" class="tc-subunit-more">Xem đầy đủ <i class="pi pi-arrow-right"></i></a>
          </div>
          <div class="tc-table-wrap">
            <p-table
              [value]="subUnits"
              styleClass="home-progress-table app-admin-table"
              [tableStyle]="{ 'min-width': '40rem' }"
              [paginator]="subUnits.length > 8"
              [rows]="8"
            >
              <ng-template pTemplate="header">
                <tr>
                  <th>Đơn vị</th>
                  <th>Trạng thái xác nhận</th>
                  <th>Tổng bản ghi</th>
                </tr>
              </ng-template>
              <ng-template pTemplate="body" let-row>
                <tr>
                  <td>{{ row.tenDonVi }}</td>
                  <td>
                    <span class="dashboard-tag" [class]="row.daXacNhan ? 'tag-da-xac-nhan' : 'tag-chua-nhap'">
                      {{ row.daXacNhan ? 'Đã xác nhận' : 'Chưa xác nhận' }}
                    </span>
                  </td>
                  <td>{{ subUnitTotal(row) }}</td>
                </tr>
              </ng-template>
              <ng-template pTemplate="emptymessage">
                <tr><td colspan="3">Chưa có đơn vị cấp dưới nào được ghi nhận.</td></tr>
              </ng-template>
            </p-table>
          </div>
        </ng-template>
      </div>

      <!-- ── MANAGER: Tiến độ kỳ báo cáo (giữ nguyên hành vi hiện tại) ── -->
      <div class="app-page-card tc-progress-card" *ngIf="dashboardVariant === 'MANAGER'">
        <app-loading-overlay
          [active]="loading"
          label="Đang tải dashboard..."
        ></app-loading-overlay>

        <div class="tc-progress-card__header">
          <span class="tc-progress-card__icon" aria-hidden="true">
            <i class="pi pi-chart-bar"></i>
          </span>
          <div class="tc-progress-card__heading">
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
              <span class="tc-stat-icon tc-stat-icon--all"><i class="pi pi-sitemap"></i></span>
              <div class="tc-stat-lbl">Tổng đơn vị</div>
              <div class="tc-stat-val">{{ progress.tongDonVi }}</div>
            </div>
            <div class="tc-stat-cell">
              <span class="tc-stat-icon tc-stat-icon--submitted"><i class="pi pi-send"></i></span>
              <div class="tc-stat-lbl">Đã nộp</div>
              <div class="tc-stat-val">{{ progress.soDonViDaNop }}</div>
            </div>
            <div class="tc-stat-cell">
              <span class="tc-stat-icon tc-stat-icon--supplement"><i class="pi pi-file-edit"></i></span>
              <div class="tc-stat-lbl">Đang bổ sung</div>
              <div class="tc-stat-val">{{ progress.soDonViDangBoSung }}</div>
            </div>
            <div class="tc-stat-cell">
              <span class="tc-stat-icon tc-stat-icon--confirmed"><i class="pi pi-check-circle"></i></span>
              <div class="tc-stat-lbl">Đã xác nhận</div>
              <div class="tc-stat-val">{{ progress.soDonViDaXacNhan }}</div>
            </div>
            <div class="tc-stat-cell tc-stat-cell--alert">
              <span class="tc-stat-icon tc-stat-icon--missing"><i class="pi pi-exclamation-circle"></i></span>
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
          <div class="tc-table-wrap" *ngIf="progress.donVis.length > 0; else emptyProgressRows">
            <p-table
              [value]="progress.donVis"
              styleClass="home-progress-table app-admin-table"
              [tableStyle]="{ 'min-width': '52rem' }"
              [paginator]="progress.donVis.length > 8"
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
                  <td>{{ row.tenDonVi }}</td>
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
                  <td>{{ (progressTimestamp(row) | date: 'dd/MM/yyyy HH:mm') || '-' }}</td>
                </tr>
              </ng-template>
            </p-table>
          </div>
          <ng-template #emptyProgressRows>
            <div class="tc-inline-empty">
              <span class="tc-inline-empty__icon"><i class="pi pi-inbox"></i></span>
              <div>
                <strong>Chưa có dữ liệu đơn vị trong kỳ này</strong>
                <p>Dữ liệu tiến độ sẽ xuất hiện khi các đơn vị bắt đầu khai báo.</p>
              </div>
            </div>
          </ng-template>
        </ng-container>

        <ng-template #noDashboardData>
          <div
            class="tc-empty-wrap"
            *ngIf="canViewMyProgress && myTienDo; else noPermissionData"
          >
            <div
              class="tc-stat-row tc-stat-row--personal"
            >
              <div class="tc-stat-cell">
                <span class="tc-stat-icon tc-stat-icon--all"><i class="pi pi-building"></i></span>
                <div class="tc-stat-lbl">Đơn vị</div>
                <div class="tc-stat-val tc-stat-val--unit">
                  {{ myTienDo.tenDonVi }}
                </div>
              </div>
              <div class="tc-stat-cell" *ngIf="myTienDo.parentIsTongHop">
                <span class="tc-stat-icon tc-stat-icon--confirmed"><i class="pi pi-verified"></i></span>
                <div class="tc-stat-lbl">Đã xác nhận với cấp trên</div>
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
                <span class="tc-stat-icon tc-stat-icon--submitted"><i class="pi pi-list-check"></i></span>
                <div class="tc-stat-lbl">Tổng bản ghi</div>
                <div class="tc-stat-val">{{ myTotalRecords }}</div>
              </div>
            </div>
            <div class="tc-personal-actions" *ngIf="myTienDo.parentIsTongHop">
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
                class="tc-dashboard-empty"
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
        <div class="home-section-heading">
          <div>
            <h2 class="home-section-title">Truy cập nhanh</h2>
            <p>Đi thẳng tới các chức năng thường dùng theo quyền của bạn.</p>
          </div>
          <div class="home-quick-actions">
            <span>{{ quickLinks.length }} chức năng</span>
            <button type="button" class="home-quick-config-button" (click)="quickConfigOpen = !quickConfigOpen" [attr.aria-expanded]="quickConfigOpen">
              <i class="pi pi-cog" aria-hidden="true"></i> Cấu hình
            </button>
          </div>
        </div>
        <div class="home-quick-config" *ngIf="quickConfigOpen">
          <p>Chọn các màn hình muốn hiển thị. Thiết lập được lưu cho tài khoản này trên trình duyệt hiện tại.</p>
          <div class="home-quick-options">
            <label *ngFor="let link of availableQuickLinks">
              <input type="checkbox" [checked]="isQuickLinkSelected(link.route)" (change)="toggleQuickLink(link.route)" />
              <span>{{ link.label }}</span>
            </label>
          </div>
          <button type="button" class="home-quick-reset" (click)="resetQuickLinks()">Khôi phục mặc định</button>
        </div>
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
        gap: 1.1rem;
      }

      /* ── Welcome bar ── */
      .tc-welcome-bar {
        position: relative;
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: 16px;
        min-height: 5rem;
        padding: 0.8rem 1.1rem;
        overflow: hidden;
        background:
          linear-gradient(100deg, #ffffff 0%, #ffffff 68%, #f3f7fb 100%);
        border: 1px solid color-mix(in srgb, var(--app-border) 88%, white 12%);
        border-radius: 8px;
        box-shadow: 0 2px 10px rgba(30, 58, 95, 0.05);
      }
      .tc-welcome-bar__left {
        display: flex;
        align-items: center;
        gap: 14px;
        min-width: 0;
      }
      .tc-welcome-bar__icon {
        width: 42px;
        height: 42px;
        border-radius: 9px;
        background: #eef3f9;
        color: var(--app-primary-500);
        display: flex;
        align-items: center;
        justify-content: center;
        flex-shrink: 0;
        font-size: 1.05rem;
        box-shadow: inset 0 0 0 1px rgba(30, 58, 95, 0.06);
      }
      .tc-welcome-bar__content {
        min-width: 0;
      }
      .tc-welcome-bar__eyebrow {
        margin-bottom: 0.1rem;
        color: #8a5b16;
        font-size: 0.63rem;
        font-weight: 750;
        letter-spacing: 0.09em;
        line-height: 1.2;
        text-transform: uppercase;
      }
      .tc-welcome-bar__title {
        font-size: 1.12rem;
        font-weight: 750;
        color: var(--app-text-strong);
      }
      .tc-welcome-bar__sub {
        font-size: 0.79rem;
        color: var(--app-text-muted);
        margin-top: 0.12rem;
      }
      .tc-btn-ky {
        flex-shrink: 0;
        white-space: nowrap;
        border-radius: 6px;
        background: rgba(255, 255, 255, 0.8);
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
        border-radius: 8px;
      }
      .tc-progress-card__header {
        display: flex;
        align-items: center;
        gap: 0.7rem;
        padding: 0.85rem 1.05rem;
        border-bottom: 1px solid var(--app-border);
      }
      .tc-progress-card__icon {
        display: inline-flex;
        flex: 0 0 2.35rem;
        align-items: center;
        justify-content: center;
        width: 2.35rem;
        height: 2.35rem;
        border-radius: 8px;
        background: #eef3f9;
        color: var(--app-primary-600);
      }
      .tc-progress-card__heading {
        min-width: 0;
      }
      .tc-card-title {
        font-size: 0.92rem;
        font-weight: 750;
        color: var(--app-text-strong);
        text-transform: uppercase;
        letter-spacing: 0.04em;
        margin: 0 0 2px;
      }
      .tc-card-sub {
        font-size: 0.78rem;
        color: var(--app-text-muted);
        margin: 0;
      }
      .tc-mode-badge {
        display: inline-flex;
        flex: 0 0 auto;
        align-items: center;
        gap: 5px;
        margin-left: auto;
        padding: 5px 10px;
        border: 1px solid #cddaea;
        border-radius: 999px;
        background: #f1f6fb;
        color: #315b83;
        font-size: 0.72rem;
        font-weight: 700;
        white-space: nowrap;
      }
      .tc-mode-badge--tonghop {
        border-color: #d8c9a3;
        background: #f8f1e0;
        color: #8a5b16;
      }

      /* ── Kỳ bar ── */
      .tc-ky-bar {
        display: flex;
        align-items: center;
        gap: 10px;
        padding: 0.6rem 1rem;
        background: #f4f7fa;
        border-bottom: 1px solid color-mix(in srgb, #c4d8ea 72%, white 28%);
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
        height: 2.35rem;
        border: 1px solid var(--app-primary-500) !important;
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
        padding: 0.45rem 0.7rem;
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
        grid-template-columns: repeat(5, minmax(0, 1fr));
        gap: 0.65rem;
        padding: 0.8rem 1rem;
        border-bottom: 1px solid var(--app-border);
        background: #fbfcfe;
      }
      .tc-stat-cell {
        display: grid;
        grid-template-columns: 2.35rem minmax(0, 1fr);
        grid-template-rows: auto auto;
        column-gap: 0.7rem;
        align-items: center;
        min-height: 4.35rem;
        padding: 0.65rem 0.75rem;
        border: 1px solid color-mix(in srgb, var(--app-border) 84%, white 16%);
        border-radius: 7px;
        background: #fff;
        box-shadow: 0 1px 4px rgba(15, 23, 42, 0.035);
      }
      .tc-stat-cell--alert {
        border-color: #efcdd2;
        background: #fff8f8;
      }
      .tc-stat-icon {
        display: inline-flex;
        grid-column: 1;
        grid-row: 1 / 3;
        align-items: center;
        justify-content: center;
        width: 2.35rem;
        height: 2.35rem;
        border-radius: 7px;
        font-size: 0.9rem;
      }
      .tc-stat-icon--all {
        background: #eef4ff;
        color: #2563eb;
      }
      .tc-stat-icon--submitted {
        background: #ecf8f3;
        color: #16835f;
      }
      .tc-stat-icon--supplement {
        background: #fff5e8;
        color: #b56805;
      }
      .tc-stat-icon--confirmed {
        background: #f2edff;
        color: #7651b5;
      }
      .tc-stat-icon--missing {
        background: #fdecef;
        color: #9a2b39;
      }
      .tc-stat-lbl {
        grid-column: 2;
        grid-row: 1;
        font-size: 0.62rem;
        font-weight: 700;
        letter-spacing: 0.07em;
        text-transform: uppercase;
        color: var(--app-text-muted);
        margin: 0 0 0.12rem;
      }
      .tc-stat-lbl--alert {
        color: #8a1f2d;
      }
      .tc-stat-val {
        grid-column: 2;
        grid-row: 2;
        min-width: 0;
        font-size: 1.5rem;
        font-weight: 750;
        color: var(--app-text-strong);
        line-height: 1;
        font-variant-numeric: tabular-nums;
      }
      .tc-stat-val--alert {
        color: #8a1f2d;
      }
      .tc-stat-row--personal {
        grid-template-columns: minmax(15rem, 1.5fr) repeat(2, minmax(0, 1fr));
        border-bottom: 0;
      }
      .tc-stat-val--unit {
        overflow: hidden;
        font-size: 0.92rem;
        line-height: 1.3;
        text-overflow: ellipsis;
        white-space: nowrap;
      }

      /* ── Progress row ── */
      .tc-prog-row {
        display: flex;
        align-items: center;
        gap: 10px;
        padding: 0.65rem 1rem;
        border-bottom: 1px solid var(--app-border);
        background: #fff;
      }
      .tc-prog-text {
        font-size: 0.76rem;
        color: var(--app-text-muted);
        white-space: nowrap;
      }
      .tc-prog-track {
        flex: 1;
        height: 7px;
        border-radius: 999px;
        background: rgba(120, 95, 55, 0.14);
        overflow: hidden;
      }
      .tc-prog-fill {
        height: 7px;
        border-radius: 999px;
        background: linear-gradient(90deg, var(--app-primary-500), #3b82b7);
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
      /* Bang tom tat tren dashboard can gon hon bang admin thong thuong
         (.app-admin-table mac dinh padding rong cho man quan tri rieng). */
      :host ::ng-deep .home-progress-table {
        .p-datatable-thead > tr > th {
          padding: 0.55rem 0.7rem;
          font-size: 0.68rem;
        }
        .p-datatable-tbody > tr > td {
          padding: 0.5rem 0.7rem;
          font-size: 0.8rem;
        }
      }
      .tc-inline-empty {
        display: flex;
        min-height: 8.5rem;
        align-items: center;
        justify-content: center;
        gap: 0.85rem;
        padding: 1.25rem;
        background: #fff;
        text-align: left;
      }
      .tc-inline-empty__icon {
        display: inline-flex;
        flex: 0 0 2.8rem;
        align-items: center;
        justify-content: center;
        width: 2.8rem;
        height: 2.8rem;
        border-radius: 50%;
        background: #eef3f9;
        color: var(--app-primary-600);
      }
      .tc-inline-empty strong {
        display: block;
        color: var(--app-text-strong);
        font-size: 0.88rem;
      }
      .tc-inline-empty p {
        margin: 0.2rem 0 0;
        color: var(--app-text-muted);
        font-size: 0.78rem;
      }
      .dashboard-tag {
        display: inline-flex;
        align-items: center;
        padding: 0.2rem 0.5rem;
        border-radius: 999px;
        border: 1px solid transparent;
        font-size: 0.72rem;
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
        padding: 1rem;
      }
      .tc-personal-actions {
        display: flex;
        justify-content: flex-end;
        padding: 1rem;
      }
      :host ::ng-deep .tc-dashboard-empty .app-empty-state {
        min-height: 10rem;
        border-color: color-mix(in srgb, var(--app-primary-500) 20%, white 80%);
        background: #fbfcfe;
      }

      /* ── Quick access ── */
      .home-section-heading {
        display: flex;
        align-items: flex-end;
        justify-content: space-between;
        gap: 1rem;
        margin-bottom: 0.65rem;
      }
      .home-section-title {
        font-size: 0.8125rem;
        font-weight: 700;
        text-transform: uppercase;
        letter-spacing: 0.05em;
        color: var(--app-text-muted);
        margin: 0;
      }
      .home-section-heading p {
        margin: 0.18rem 0 0;
        color: var(--app-text-muted);
        font-size: 0.75rem;
      }
      .home-section-heading > span {
        flex-shrink: 0;
        color: var(--app-text-muted);
        font-size: 0.72rem;
        font-weight: 600;
      }
      .home-quick-actions { display: flex; align-items: center; gap: 0.75rem; color: var(--app-text-muted); font-size: 0.72rem; font-weight: 600; }
      .home-quick-config-button, .home-quick-reset { border: 1px solid var(--app-border); border-radius: 6px; background: #fff; color: var(--app-primary-600); padding: 0.35rem 0.6rem; cursor: pointer; font: inherit; }
      .home-quick-config { padding: 0.8rem; margin-bottom: 0.7rem; background: #fff; border: 1px solid var(--app-border); border-radius: 8px; }
      .home-quick-config p { margin: 0 0 0.65rem; color: var(--app-text-muted); font-size: 0.76rem; }
      .home-quick-options { display: grid; grid-template-columns: repeat(auto-fill, minmax(190px, 1fr)); gap: 0.5rem; max-height: 280px; overflow: auto; }
      .home-quick-options label { display: flex; align-items: center; gap: 0.5rem; font-size: 0.8rem; cursor: pointer; }
      .home-quick-reset { margin-top: 0.8rem; }
      .tc-qa-grid {
        display: grid;
        grid-template-columns: repeat(3, 1fr);
        gap: 0.65rem;
      }
      .tc-qa-card {
        position: relative;
        background: var(--app-surface);
        border: 1px solid color-mix(in srgb, var(--app-border) 86%, white 14%);
        border-radius: 8px;
        padding: 0.8rem 0.9rem;
        display: flex;
        align-items: center;
        gap: 0.75rem;
        min-height: 4.15rem;
        text-decoration: none;
        color: var(--app-text);
        transition:
          background 150ms ease,
          border-color 150ms ease,
          box-shadow 150ms ease,
          transform 150ms ease;
      }
      .tc-qa-card:hover {
        background: #fff;
        border-color: color-mix(in srgb, var(--app-primary-500) 30%, white 70%);
        box-shadow: 0 6px 16px rgba(30, 58, 95, 0.08);
        transform: translateY(-1px);
      }
      .tc-qa-icon {
        width: 38px;
        height: 38px;
        border-radius: 8px;
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
        font-size: 0.86rem;
        font-weight: 650;
        color: var(--app-text-strong);
      }
      .tc-qa-sub {
        font-size: 0.73rem;
        color: var(--app-text-muted);
        margin-top: 1px;
      }
      .tc-qa-arr {
        font-size: 0.68rem;
        color: #c8bfb2;
        flex-shrink: 0;
        transition:
          color 150ms ease,
          transform 150ms ease;
      }
      .tc-qa-card:hover .tc-qa-arr {
        color: var(--app-primary-500);
        transform: translateX(2px);
      }
      .tc-qa-card:nth-child(3n + 2) .tc-qa-icon {
        background: #f2edff;
        color: #7651b5;
      }
      .tc-qa-card:nth-child(3n) .tc-qa-icon {
        background: #ecf8f3;
        color: #16835f;
      }

      /* ── Module breakdown grid (UNIT variant) - dang the giong man Hien trang ── */
      .tc-module-grid {
        display: grid;
        grid-template-columns: repeat(auto-fill, minmax(150px, 1fr));
        gap: 0.6rem;
        padding: 0.9rem 1rem;
        border-bottom: 1px solid var(--app-border);
        background: #fff;
      }
      .tc-module-card {
        min-width: 0;
        padding: 0.7rem 0.8rem;
        border: 1px solid color-mix(in srgb, var(--app-border) 84%, white 16%);
        border-radius: 8px;
        background: #fbfcfe;
      }
      .tc-module-card--empty {
        opacity: 0.6;
      }
      .tc-module-card__title {
        display: flex;
        align-items: center;
        gap: 0.4rem;
        margin-bottom: 0.4rem;
        min-width: 0;
      }
      .tc-module-card__title span:last-child {
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
        font-size: 0.72rem;
        font-weight: 600;
        color: var(--app-text-muted);
      }
      .tc-module-card__icon {
        display: grid;
        flex: 0 0 auto;
        place-items: center;
        width: 22px;
        height: 22px;
        border-radius: 6px;
        background: #eef3f9;
        color: var(--app-primary-600);
        font-size: 0.62rem;
      }
      .tc-module-card__val {
        font-size: 1.25rem;
        font-weight: 800;
        color: var(--app-text-strong);
        line-height: 1.1;
        font-variant-numeric: tabular-nums;
      }
      .tc-module-card small {
        color: var(--app-text-muted);
        font-size: 0.66rem;
      }

      /* ── Subordinate units (UNIT variant, che do tong hop) ── */
      .tc-subunit-stat-row {
        display: flex;
        flex-wrap: wrap;
        align-items: center;
        gap: 1.4rem;
        padding: 0.75rem 1rem 0.7rem;
      }
      .tc-subunit-stat {
        display: flex;
        align-items: baseline;
        gap: 0.35rem;
      }
      .tc-subunit-stat__val {
        font-size: 1.05rem;
        font-weight: 750;
        color: var(--app-text-strong);
        font-variant-numeric: tabular-nums;
      }
      .tc-subunit-stat__lbl {
        color: var(--app-text-muted);
        font-size: 0.74rem;
      }
      .tc-subunit-stat--alert .tc-subunit-stat__val {
        color: #9a2b39;
      }
      .tc-subunit-more {
        margin-left: auto;
        color: var(--app-primary-500);
        font-size: 0.76rem;
        font-weight: 600;
        text-decoration: none;
        white-space: nowrap;
      }
      .tc-subunit-more:hover {
        text-decoration: underline;
      }

      @media (max-width: 900px) {
        .tc-stat-row {
          grid-template-columns: repeat(3, 1fr);
        }
        .tc-stat-row--personal {
          grid-template-columns: 1fr 1fr;
        }
        .tc-stat-row--personal .tc-stat-cell:first-child {
          grid-column: 1 / -1;
        }
        .tc-qa-grid {
          grid-template-columns: repeat(2, 1fr);
        }
        .tc-welcome-bar {
          flex-direction: column;
          align-items: flex-start;
        }
        .tc-ky-bar {
          flex-wrap: wrap;
        }
        .tc-btn-refresh {
          margin-left: 0;
        }
      }
      @media (max-width: 600px) {
        .home-page {
          gap: 0.85rem;
        }
        .tc-welcome-bar {
          padding: 0.8rem;
        }
        .tc-btn-ky {
          width: 100%;
          justify-content: center;
        }
        .tc-progress-card__header {
          align-items: flex-start;
          padding: 0.8rem;
        }
        .tc-ky-bar {
          align-items: stretch;
          padding: 0.7rem 0.8rem;
        }
        .tc-ky-bar__sep {
          display: none;
        }
        .tc-ky-bar__group {
          width: 100%;
          flex-wrap: wrap;
        }
        :host ::ng-deep .tc-ky-bar .p-dropdown {
          width: 100% !important;
          min-width: 0;
          max-width: none;
        }
        .tc-btn-refresh {
          width: 100%;
        }
        .tc-stat-row {
          grid-template-columns: repeat(2, 1fr);
          padding: 0.65rem;
        }
        .tc-stat-row--personal {
          grid-template-columns: 1fr;
        }
        .tc-stat-row--personal .tc-stat-cell:first-child {
          grid-column: auto;
        }
        .tc-qa-grid {
          grid-template-columns: 1fr;
        }
        .home-section-heading {
          align-items: flex-start;
          flex-direction: column;
          gap: 0.25rem;
        }
        .tc-prog-row {
          flex-wrap: wrap;
        }
        .tc-prog-track {
          order: 3;
          flex-basis: 100%;
        }
      }
    `,
  ],
})
export class HomePage {
  quickConfigOpen = false;
  private selectedQuickRoutes: string[] | null = null;
  selectedKyId: number | null = null;
  selectedKyCode: string | null = null;
  progress: KyBaoCaoTienDoDto | null = null;
  myTienDo: TienDoDonViDto | null = null;
  subUnits: TienDoDonViDto[] = [];
  adminStats: AdminStats | null = null;
  loadingAdminStats = false;
  loading = false;

  /** 17 module nghiệp vụ — code khớp mẫu báo cáo, key khớp field TienDoDonViDto. */
  private readonly moduleDefs: Array<{
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
    ...EXTRA_QUICK_LINKS,
  ];

  private kyItems: KyBaoCaoDto[] = [];

  constructor(
    public readonly authService: AuthService,
    public readonly donViModeService: DonViModeService,
    private readonly kyBaoCaoApi: KyBaoCaoApi,
    private readonly tongHopTienDoApi: TongHopTienDoApi,
    private readonly identityAdminApi: IdentityAdminApi,
    private readonly mauBaoCaoApi: MauBaoCaoApi,
    private readonly notificationService: NotificationService,
  ) {
    this.selectedQuickRoutes = loadQuickLinkRoutes(this.authService.profile());
    void this.donViModeService.ensureLoaded();
    if (this.dashboardVariant === 'ADMIN') {
      void this.loadAdminStats();
    } else {
      void this.load();
    }
  }

  /** Biến thể dashboard theo vai trò: quản trị hệ thống / quản lý chung / đơn vị (cấp tỉnh, cấp xã). */
  get dashboardVariant(): 'ADMIN' | 'MANAGER' | 'UNIT' {
    if (this.authService.hasRole('SYSTEM_ADMIN')) return 'ADMIN';
    if (this.authService.hasRole('CAP_TINH') || this.authService.hasRole('CAP_XA')) {
      return 'UNIT';
    }
    return 'MANAGER';
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

  get canManageKy(): boolean {
    return this.authService.hasPermission('ky_bao_cao:read');
  }

  get unitModuleBreakdown(): Array<{ code: string; label: string; count: number }> {
    return this.moduleDefs.map((def) => ({
      code: def.code,
      label: def.label,
      count: this.myTienDo ? ((this.myTienDo[def.key] as number) ?? 0) : 0,
    }));
  }

  subUnitTotal(unit: TienDoDonViDto): number {
    return this.moduleDefs.reduce(
      (sum, def) => sum + ((unit[def.key] as number) ?? 0),
      0,
    );
  }

  get subUnitStats(): { confirmed: number; unconfirmed: number } {
    const confirmed = this.subUnits.filter((u) => u.daXacNhan).length;
    return { confirmed, unconfirmed: this.subUnits.length - confirmed };
  }

  get subUnitRecordTotal(): number {
    return this.subUnits.reduce((sum, u) => sum + this.subUnitTotal(u), 0);
  }

  get availableQuickLinks(): QuickLink[] {
    const isTongHop = this.donViModeService.isTongHop;
    return this.allQuickLinks.filter(
      (link) => {
        const requiredPermission = getRoutePermission(link.route);
        return (
          !!requiredPermission &&
          this.authService.hasPermission(requiredPermission) &&
          !(isTongHop && link.hideWhenTongHop)
        );
      },
    );
  }

  get quickLinks(): QuickLink[] {
    const available = this.availableQuickLinks;
    if (this.selectedQuickRoutes === null) {
      return available.filter((link) => DEFAULT_QUICK_ROUTES.includes(link.route));
    }
    return this.selectedQuickRoutes
      .map((route) => available.find((link) => link.route === route))
      .filter((link): link is QuickLink => !!link);
  }

  isQuickLinkSelected(route: string): boolean {
    return this.quickLinks.some((link) => link.route === route);
  }

  toggleQuickLink(route: string): void {
    const selected = this.selectedQuickRoutes ?? [...DEFAULT_QUICK_ROUTES];
    this.selectedQuickRoutes = selected.includes(route)
      ? selected.filter((item) => item !== route)
      : [...selected, route];
    localStorage.setItem(quickLinkStorageKey(this.authService.profile()), JSON.stringify(this.selectedQuickRoutes));
  }

  resetQuickLinks(): void {
    this.selectedQuickRoutes = null;
    localStorage.removeItem(quickLinkStorageKey(this.authService.profile()));
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
      const canReadKy = this.canManageKy;

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

      this.subUnits = await this.loadSubUnits();
    } finally {
      this.loading = false;
    }
  }

  /** Đơn vị cấp dưới trực tiếp (vd. cấp tỉnh xem các đơn vị cấp xã trực thuộc), nếu có. */
  private async loadSubUnits(): Promise<TienDoDonViDto[]> {
    if (
      this.dashboardVariant !== 'UNIT' ||
      !this.selectedKyCode ||
      !this.authService.hasPermission('tong_hop_tien_do:read')
    ) {
      return [];
    }
    return this.tongHopTienDoApi
      .getTienDo(this.selectedKyCode)
      .catch(() => [] as TienDoDonViDto[]);
  }

  private async loadAdminStats(): Promise<void> {
    this.loadingAdminStats = true;
    try {
      const [users, roles, donVis, mauBaoCao] = await Promise.all([
        this.identityAdminApi.getUsers().catch(() => [] as UserDto[]),
        this.identityAdminApi.getRoles().catch(() => [] as RoleDto[]),
        this.identityAdminApi.getDonVis().catch(() => [] as DonViDto[]),
        this.mauBaoCaoApi.getAll(true).catch(() => [] as MauBaoCaoDto[]),
      ]);
      this.adminStats = {
        totalUsers: users.length,
        activeUsers: users.filter((u) => u.isActive).length,
        totalDonVi: donVis.length,
        totalRoles: roles.length,
        totalMauBaoCao: mauBaoCao.length,
      };
    } finally {
      this.loadingAdminStats = false;
    }
  }

  async onKyChange(value: number | null | undefined): Promise<void> {
    if (!this.authService.hasPermission('ky_bao_cao:read')) {
      this.selectedKyId = null;
      this.selectedKyCode = null;
      this.progress = null;
      this.myTienDo = null;
      this.subUnits = [];
      return;
    }

    this.selectedKyId = value ?? null;
    this.selectedKyCode = this.selectedKy?.kyCode ?? null;
    if (!this.selectedKyId) {
      this.progress = null;
      this.myTienDo = null;
      this.subUnits = [];
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

      this.subUnits = await this.loadSubUnits();
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

  progressTimestamp(row: KyBaoCaoTienDoDonViDto): string | null {
    return row.lockedAt || row.submittedAt || row.ngayMoLai || row.ngayXacNhan || null;
  }
}
