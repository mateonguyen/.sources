import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { ButtonModule } from 'primeng/button';

type EmptyStateButtonSeverity =
  | 'success'
  | 'info'
  | 'warning'
  | 'danger'
  | 'help'
  | 'primary'
  | 'secondary'
  | 'contrast';

@Component({
  selector: 'app-empty-state',
  standalone: true,
  imports: [CommonModule, ButtonModule],
  template: `
    <section class="app-empty-state">
      <i class="app-empty-state-icon pi" [ngClass]="icon"></i>
      <h3>{{ title }}</h3>
      <p>{{ message }}</p>
      <button
        pButton
        type="button"
        *ngIf="actionLabel"
        [label]="actionLabel"
        [icon]="actionIcon"
        [severity]="actionSeverity || undefined"
        [class]="actionStyleClass"
        (click)="action.emit()"
      ></button>
    </section>
  `,
  styles: [
    `
      .app-empty-state {
        padding: 1.4rem;
        border: 1px dashed var(--app-border);
        border-radius: var(--app-radius-lg);
        background: var(--app-surface-soft);
        text-align: center;
      }

      .app-empty-state h3 {
        margin: 0.45rem 0;
      }

      .app-empty-state p {
        margin: 0 0 0.9rem;
        color: var(--app-text-muted);
      }

      .app-empty-state-icon {
        font-size: 1.35rem;
        color: var(--app-secondary-500);
      }
    `,
  ],
})
export class EmptyStateComponent {
  @Input() title = 'Khong co du lieu';
  @Input() message = 'Du lieu se hien thi tai day khi san sang.';
  @Input() icon = 'pi-inbox';
  @Input() actionLabel = '';
  @Input() actionIcon = 'pi pi-refresh';
  @Input() actionSeverity: EmptyStateButtonSeverity | null = 'secondary';
  @Input() actionStyleClass = '';
  @Output() action = new EventEmitter<void>();
}
