import { Component, effect, inject, signal } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ConfirmDialogWrapperService } from './confirm-dialog-wrapper.service';

@Component({
  selector: 'app-confirm-dialog-wrapper',
  standalone: true,
  imports: [ConfirmDialogModule, ButtonModule],
  template: `
    <p-confirmDialog
      #cd
      styleClass="app-confirm-dialog"
      [style]="{ width: '28rem' }"
      (onHide)="onHide()"
    >
      <ng-template pTemplate="footer">
        <button
          pButton
          type="button"
          [label]="cd.rejectButtonLabel"
          [class]="cd.option('rejectButtonStyleClass')"
          (click)="cd.reject()"
        ></button>
        <button
          pButton
          type="button"
          [label]="acceptLabel(cd.acceptButtonLabel)"
          [class]="cd.option('acceptButtonStyleClass')"
          [disabled]="remaining() > 0"
          (click)="cd.accept()"
        ></button>
      </ng-template>
    </p-confirmDialog>
  `,
})
export class ConfirmDialogWrapperComponent {
  private readonly dialogService = inject(ConfirmDialogWrapperService);

  readonly remaining = signal(0);
  private timerHandle: ReturnType<typeof setInterval> | null = null;

  constructor() {
    // Khi service bao co dem nguoc (thao tac nguy hiem, vd xoa), khoa nut
    // dong y trong tung nay giay de tranh bam nham.
    // allowSignalWrites: effect nay chu dong ghi lai signal "remaining" ben
    // trong - Angular chan ghi signal trong effect mac dinh, phai bat co nay
    // thi remaining.set(...) moi thuc su chay (thieu co nay la nguyen nhan
    // dem nguoc khong hoat dong truoc do).
    effect(
      () => {
        const seconds = this.dialogService.countdownSeconds();
        this.clearTimer();

        if (seconds && seconds > 0) {
          this.remaining.set(seconds);
          this.timerHandle = setInterval(() => {
            const next = this.remaining() - 1;
            if (next <= 0) {
              this.remaining.set(0);
              this.clearTimer();
            } else {
              this.remaining.set(next);
            }
          }, 1000);
        } else {
          this.remaining.set(0);
        }
      },
      { allowSignalWrites: true },
    );
  }

  acceptLabel(baseLabel: string): string {
    const seconds = this.remaining();
    return seconds > 0 ? `${baseLabel} (${seconds})` : baseLabel;
  }

  onHide(): void {
    this.clearTimer();
    this.remaining.set(0);
  }

  private clearTimer(): void {
    if (this.timerHandle !== null) {
      clearInterval(this.timerHandle);
      this.timerHandle = null;
    }
  }
}
