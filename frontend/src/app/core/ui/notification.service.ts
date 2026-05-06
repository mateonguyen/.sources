import { Injectable, inject } from '@angular/core';
import { MessageService } from 'primeng/api';

export interface AppNotification {
  id: number;
  type: 'success' | 'error' | 'warning' | 'info';
  message: string;
}

@Injectable({ providedIn: 'root' })
export class NotificationService {
  private readonly messageService = inject(MessageService);

  show(type: AppNotification['type'], message: string, timeoutMs = 4000): void {
    this.messageService.add({
      severity: type === 'warning' ? 'warn' : type,
      summary: this.summaryFor(type),
      detail: message,
      life: timeoutMs,
    });
  }

  dismiss(_id: number): void {
    this.messageService.clear();
  }

  private summaryFor(type: AppNotification['type']): string {
    if (type === 'success') {
      return 'Thành công';
    }
    if (type === 'error') {
      return 'Lỗi';
    }
    if (type === 'warning') {
      return 'Cảnh báo';
    }

    return 'Thông báo';
  }
}
