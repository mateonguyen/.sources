import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-page-header',
  standalone: true,
  imports: [CommonModule],
  template: `
    <header class="app-page-header" [class.compact]="compact">
      <div>
        <h2 class="app-page-title">{{ title }}</h2>
        <p class="app-page-subtitle" *ngIf="subtitle">{{ subtitle }}</p>
      </div>
      <div class="app-page-header-actions">
        <ng-content></ng-content>
      </div>
    </header>
  `,
  styles: [
    `
      .app-page-header {
        display: flex;
        align-items: flex-start;
        justify-content: space-between;
        gap: 1rem;
        flex-wrap: wrap;
      }

      .app-page-header.compact {
        gap: 0.65rem;
      }

      .app-page-header-actions {
        display: flex;
        align-items: center;
        gap: 0.6rem;
        flex-wrap: wrap;
      }
    `,
  ],
})
export class PageHeaderComponent {
  @Input({ required: true }) title = '';
  @Input() subtitle = '';
  @Input() compact = false;
}
