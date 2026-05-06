import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';
import { ProgressSpinnerModule } from 'primeng/progressspinner';

@Component({
  selector: 'app-loading-overlay',
  standalone: true,
  imports: [CommonModule, ProgressSpinnerModule],
  template: `
    <div class="app-loading-overlay" *ngIf="active">
      <p-progressSpinner
        strokeWidth="6"
        [style]="{ width: spinnerSize, height: spinnerSize }"
      ></p-progressSpinner>
      <span>{{ label }}</span>
    </div>
  `,
  styles: [
    `
      .app-loading-overlay {
        position: absolute;
        inset: 0;
        border-radius: inherit;
        background: rgba(255, 255, 255, 0.72);
        backdrop-filter: blur(2px);
        z-index: 11;
        display: flex;
        align-items: center;
        justify-content: center;
        gap: var(--app-space-3);
        color: var(--app-text-muted);
        font-size: var(--app-font-size-sm);
        font-weight: var(--app-font-weight-medium);
      }
    `,
  ],
})
export class LoadingOverlayComponent {
  @Input() active = false;
  @Input() label = 'Dang tai du lieu...';
  @Input() spinnerSize = '24px';
}
