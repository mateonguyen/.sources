import { Injectable, inject, signal } from '@angular/core';
import { ConfirmationService } from 'primeng/api';

export type ConfirmDialogIntent =
  | 'default'
  | 'approve'
  | 'reject'
  | 'delete'
  | 'deactivate'
  | 'reopen'
  | 'request-supplement'
  | 'submit'
  | 'reset-password';

export interface ConfirmDialogOptions {
  header?: string;
  message: string;
  icon?: string;
  intent?: ConfirmDialogIntent;
  acceptLabel?: string;
  rejectLabel?: string;
  acceptButtonStyleClass?: string;
  rejectButtonStyleClass?: string;
  /** Neu > 0, nut dong y bi khoa trong tung nay giay (dem nguoc) truoc khi
   * bam duoc - dung cho thao tac nguy hiem/kho hoan tac (vd xoa). */
  countdownSeconds?: number;
}

type ConfirmDialogPresetOptions = Omit<ConfirmDialogOptions, 'intent'>;

interface ConfirmDialogAppearance {
  icon: string;
  acceptButtonStyleClass?: string;
  rejectButtonStyleClass: string;
}

@Injectable({ providedIn: 'root' })
export class ConfirmDialogWrapperService {
  private readonly confirmationService = inject(ConfirmationService);

  /** Doc boi ConfirmDialogWrapperComponent de biet co dang can dem nguoc
   * truoc khi cho bam nut dong y hay khong (null = khong dem nguoc). */
  readonly countdownSeconds = signal<number | null>(null);

  confirm(options: ConfirmDialogOptions): Promise<boolean> {
    const intent = options.intent ?? this.inferIntent(options);
    const appearance = this.resolveAppearance(intent);

    return new Promise((resolve) => {
      this.countdownSeconds.set(
        options.countdownSeconds && options.countdownSeconds > 0
          ? options.countdownSeconds
          : null,
      );

      const finish = (result: boolean) => {
        this.countdownSeconds.set(null);
        resolve(result);
      };

      this.confirmationService.confirm({
        header: options.header ?? 'Xác nhận thao tác',
        message: options.message,
        icon: options.icon ?? appearance.icon,
        acceptLabel: options.acceptLabel ?? 'Đồng ý',
        rejectLabel: options.rejectLabel ?? 'Hủy',
        acceptButtonStyleClass:
          options.acceptButtonStyleClass ?? appearance.acceptButtonStyleClass,
        rejectButtonStyleClass:
          options.rejectButtonStyleClass ?? appearance.rejectButtonStyleClass,
        accept: () => finish(true),
        reject: () => finish(false),
      });
    });
  }

  confirmApprove(options: ConfirmDialogPresetOptions): Promise<boolean> {
    return this.confirmWithIntent('approve', options);
  }

  confirmReject(options: ConfirmDialogPresetOptions): Promise<boolean> {
    return this.confirmWithIntent('reject', options);
  }

  confirmDelete(options: ConfirmDialogPresetOptions): Promise<boolean> {
    return this.confirmWithIntent('delete', options);
  }

  confirmDeactivate(options: ConfirmDialogPresetOptions): Promise<boolean> {
    return this.confirmWithIntent('deactivate', options);
  }

  confirmReopen(options: ConfirmDialogPresetOptions): Promise<boolean> {
    return this.confirmWithIntent('reopen', options);
  }

  confirmRequestSupplement(
    options: ConfirmDialogPresetOptions,
  ): Promise<boolean> {
    return this.confirmWithIntent('request-supplement', options);
  }

  confirmSubmit(options: ConfirmDialogPresetOptions): Promise<boolean> {
    return this.confirmWithIntent('submit', options);
  }

  confirmResetPassword(options: ConfirmDialogPresetOptions): Promise<boolean> {
    return this.confirmWithIntent('reset-password', options);
  }

  private confirmWithIntent(
    intent: ConfirmDialogIntent,
    options: ConfirmDialogPresetOptions,
  ): Promise<boolean> {
    return this.confirm({ ...options, intent });
  }

  private inferIntent(options: ConfirmDialogOptions): ConfirmDialogIntent {
    const source = this.normalizeForMatch(
      `${options.header ?? ''} ${options.message} ${options.acceptLabel ?? ''}`,
    );

    if (source.includes('tu choi') || source.includes('reject')) {
      return 'reject';
    }

    if (
      source.includes('phe duyet') ||
      source.includes('duyet') ||
      source.includes('approve')
    ) {
      return 'approve';
    }

    if (source.includes('mo lai') || source.includes('reopen')) {
      return 'reopen';
    }

    if (
      source.includes('bo sung') ||
      source.includes('supplement') ||
      source.includes('yeu cau bo sung')
    ) {
      return 'request-supplement';
    }

    if (
      source.includes('nop bao cao') ||
      source.includes('gui') ||
      source.includes('submit')
    ) {
      return 'submit';
    }

    if (
      source.includes('dat lai mat khau') ||
      source.includes('reset password') ||
      source.includes('reset mat khau')
    ) {
      return 'reset-password';
    }

    if (
      source.includes('ngung hoat dong') ||
      source.includes('vo hieu hoa') ||
      source.includes('deactivate')
    ) {
      return 'deactivate';
    }

    if (
      source.includes('xoa') ||
      source.includes('trash') ||
      source.includes('remove') ||
      source.includes('delete')
    ) {
      return 'delete';
    }

    return 'default';
  }

  private resolveAppearance(
    intent: ConfirmDialogIntent,
  ): ConfirmDialogAppearance {
    const rejectButtonStyleClass = 'p-button-secondary p-button-outlined';

    switch (intent) {
      case 'approve':
        return {
          icon: 'pi pi-check-circle',
          acceptButtonStyleClass: 'p-button-success',
          rejectButtonStyleClass,
        };
      case 'reject':
      case 'delete':
      case 'deactivate':
        return {
          icon: 'pi pi-exclamation-triangle',
          acceptButtonStyleClass: 'p-button-danger',
          rejectButtonStyleClass,
        };
      case 'reopen':
        return {
          icon: 'pi pi-history',
          acceptButtonStyleClass: 'p-button-warning',
          rejectButtonStyleClass,
        };
      case 'request-supplement':
        return {
          icon: 'pi pi-exclamation-circle',
          acceptButtonStyleClass: 'p-button-warning',
          rejectButtonStyleClass,
        };
      case 'submit':
        return {
          icon: 'pi pi-send',
          acceptButtonStyleClass: 'p-button-success',
          rejectButtonStyleClass,
        };
      case 'reset-password':
        return {
          icon: 'pi pi-key',
          acceptButtonStyleClass: 'p-button-info',
          rejectButtonStyleClass,
        };
      default:
        return {
          icon: 'pi pi-exclamation-triangle',
          rejectButtonStyleClass,
        };
    }
  }

  private normalizeForMatch(value: string): string {
    return value
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .toLowerCase();
  }
}
