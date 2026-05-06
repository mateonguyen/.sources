import { Component } from '@angular/core';

@Component({
  selector: 'app-filter-bar',
  standalone: true,
  template: `
    <div class="app-filter-bar">
      <ng-content></ng-content>
    </div>
  `,
  styles: [
    `
      .app-filter-bar {
        display: grid;
        gap: 0.75rem;
        padding: 0.9rem;
        border: 1px solid var(--app-border);
        border-radius: var(--app-radius-md);
        background: var(--app-surface-soft);
      }
    `,
  ],
})
export class FilterBarComponent {}
