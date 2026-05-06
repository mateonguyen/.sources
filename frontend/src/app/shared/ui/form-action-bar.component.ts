import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-form-action-bar',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div
      class="app-form-action-bar"
      [class.end]="align === 'end'"
      [class.between]="align === 'between'"
    >
      <ng-content></ng-content>
    </div>
  `,
  styles: [
    `
      .app-form-action-bar {
        display: flex;
        align-items: center;
        gap: var(--app-space-3);
        flex-wrap: wrap;
      }

      .app-form-action-bar.end {
        justify-content: flex-end;
      }

      .app-form-action-bar.between {
        justify-content: space-between;
      }
    `,
  ],
})
export class FormActionBarComponent {
  @Input() align: 'start' | 'end' | 'between' = 'start';
}
