import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ToastModule } from 'primeng/toast';
import { ConfirmationService, MessageService } from 'primeng/api';
import { AuthService } from '../../core/auth/auth.service';
import { Session } from '../../core/auth/auth.models';

@Component({
  selector: 'app-sessions-management',
  standalone: true,
  imports: [
    CommonModule,
    ButtonModule,
    TableModule,
    ConfirmDialogModule,
    ToastModule,
  ],
  providers: [ConfirmationService, MessageService],
  template: `
    <div class="sessions-container">
      <h2>Quản lý phiên đăng nhập</h2>
      <p class="sessions-description">
        Xem các thiết bị đang hoạt động và logout từ các phiên không cần thiết.
      </p>

      <div class="sessions-toolbar">
        <button
          pButton
          type="button"
          label="Logout tất cả thiết bị"
          icon="pi pi-sign-out"
          (click)="confirmLogoutAll()"
          severity="danger"
          [disabled]="loading"
        ></button>
      </div>

      <p-table
        [value]="sessions"
        styleClass="sessions-table"
        [loading]="loading"
        responsiveLayout="scroll"
      >
        <ng-template pTemplate="header">
          <tr>
            <th>Thiết bị</th>
            <th>Trình duyệt / IP</th>
            <th>Lần cuối sử dụng</th>
            <th>Hết hạn</th>
            <th>Hành động</th>
          </tr>
        </ng-template>

        <ng-template pTemplate="body" let-session>
          <tr [class.current-session]="session.isCurrentSession">
            <td>
              <div class="device-info">
                <span class="device-name">{{
                  session.deviceName || 'Thiết bị'
                }}</span>
                <span class="device-badge" *ngIf="session.isCurrentSession"
                  >Thiết bị hiện tại</span
                >
              </div>
            </td>
            <td>
              <div class="browser-ip">
                <small>{{ parseBrowserName(session.deviceUserAgent) }}</small>
                <small class="ip-address">{{ session.deviceIpAddress }}</small>
              </div>
            </td>
            <td>
              <small>{{
                session.lastUsedAt
                  ? (session.lastUsedAt | date: 'dd/MM/yyyy HH:mm')
                  : 'Chưa sử dụng'
              }}</small>
            </td>
            <td>
              <small>{{ session.expiresAt | date: 'dd/MM/yyyy' }}</small>
            </td>
            <td>
              <button
                pButton
                type="button"
                icon="pi pi-times"
                class="p-button-rounded p-button-danger p-button-text"
                (click)="confirmLogout(session)"
                [disabled]="loading || session.isCurrentSession"
                pTooltip="Logout từ thiết bị này"
                tooltipPosition="top"
              ></button>
            </td>
          </tr>
        </ng-template>

        <ng-template pTemplate="emptymessage">
          <tr>
            <td colspan="5" class="text-center">
              <p>Không có phiên đang hoạt động.</p>
            </td>
          </tr>
        </ng-template>
      </p-table>
    </div>

    <p-toast position="top-right"></p-toast>
    <p-confirmDialog></p-confirmDialog>
  `,
  styles: [
    `
      .sessions-container {
        padding: 1.5rem;
      }

      h2 {
        margin: 0 0 0.5rem;
        font-size: 1.5rem;
        color: var(--app-primary-700);
      }

      .sessions-description {
        margin: 0 0 1.5rem;
        color: var(--app-primary-600);
        font-size: 0.95rem;
      }

      .sessions-toolbar {
        margin-bottom: 1.5rem;
        display: flex;
        gap: 0.75rem;
      }

      .sessions-table {
        width: 100%;
        margin-bottom: 1.5rem;
      }

      .device-info {
        display: flex;
        align-items: center;
        gap: 0.5rem;
      }

      .device-name {
        font-weight: 600;
        color: var(--app-text-strong);
      }

      .device-badge {
        display: inline-block;
        padding: 0.25rem 0.75rem;
        background: var(--app-success-600);
        color: white;
        border-radius: 6px;
        font-size: 0.75rem;
        font-weight: 600;
        text-transform: uppercase;
      }

      .browser-ip {
        display: flex;
        flex-direction: column;
        gap: 0.25rem;
      }

      .ip-address {
        color: var(--app-primary-600);
      }

      .text-center {
        text-align: center;
      }

      tr.current-session {
        background-color: rgba(0, 150, 136, 0.05) !important;
      }
    `,
  ],
})
export class SessionsManagementComponent implements OnInit {
  sessions: Session[] = [];
  loading = false;

  constructor(
    private readonly authService: AuthService,
    private readonly confirmationService: ConfirmationService,
    private readonly messageService: MessageService,
  ) {}

  ngOnInit(): void {
    this.loadSessions();
  }

  async loadSessions(): Promise<void> {
    this.loading = true;
    try {
      this.sessions = await this.authService.getSessions();
    } catch (error) {
      this.messageService.add({
        severity: 'error',
        summary: 'Lỗi',
        detail: 'Không thể tải danh sách phiên.',
      });
    } finally {
      this.loading = false;
    }
  }

  confirmLogout(session: Session): void {
    this.confirmationService.confirm({
      message: `Bạn muốn logout khỏi thiết bị "${session.deviceName || 'Thiết bị'}" không?`,
      header: 'Xác nhận',
      icon: 'pi pi-exclamation-triangle',
      accept: () => this.logout(session.id),
    });
  }

  confirmLogoutAll(): void {
    this.confirmationService.confirm({
      message:
        'Bạn muốn logout khỏi TẤT CẢ thiết bị không? Bạn sẽ phải đăng nhập lại.',
      header: 'Xác nhận',
      icon: 'pi pi-exclamation-triangle',
      accept: () => this.logoutAll(),
    });
  }

  private async logout(sessionId: number): Promise<void> {
    this.loading = true;
    try {
      await this.authService.logout(false);
      this.messageService.add({
        severity: 'success',
        summary: 'Thành công',
        detail: 'Đã logout khỏi thiết bị.',
      });
      await this.loadSessions();
    } catch (error) {
      this.messageService.add({
        severity: 'error',
        summary: 'Lỗi',
        detail: 'Không thể logout từ thiết bị.',
      });
    } finally {
      this.loading = false;
    }
  }

  private async logoutAll(): Promise<void> {
    this.loading = true;
    try {
      await this.authService.logout(true);
      this.messageService.add({
        severity: 'success',
        summary: 'Thành công',
        detail: 'Đã logout khỏi tất cả thiết bị. Vui lòng đăng nhập lại.',
      });
      // Redirect sẽ xảy ra trong AuthService.logout()
    } catch (error) {
      this.messageService.add({
        severity: 'error',
        summary: 'Lỗi',
        detail: 'Không thể logout khỏi tất cả thiết bị.',
      });
    } finally {
      this.loading = false;
    }
  }

  /**
   * Helper: Extract browser name từ User-Agent.
   */
  parseBrowserName(userAgent: string): string {
    if (userAgent.includes('Chrome')) return 'Chrome';
    if (userAgent.includes('Safari')) return 'Safari';
    if (userAgent.includes('Firefox')) return 'Firefox';
    if (userAgent.includes('Edge')) return 'Edge';
    return 'Trình duyệt';
  }
}
