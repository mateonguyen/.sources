import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';
import { CardModule } from 'primeng/card';
import { PageHeaderComponent } from './page-header.component';

@Component({
  selector: 'app-section-card',
  standalone: true,
  imports: [CommonModule, CardModule, PageHeaderComponent],
  template: `
    <p-card [styleClass]="cardClass">
      <app-page-header *ngIf="title && headerType === 'page'" [title]="title" [subtitle]="subtitle">
        <ng-content select="[sectionActions]"></ng-content>
      </app-page-header>
      <header *ngIf="title && headerType === 'section'" class="app-section-heading">
        <div>
          <h2>{{ title }}</h2>
          <p *ngIf="subtitle">{{ subtitle }}</p>
        </div>
      </header>
      <ng-content></ng-content>
    </p-card>
  `,
  styles: [
    `
      :host {
        display: block;
      }

      :host ::ng-deep .p-card-content {
        padding-top: 0.5rem;
      }

      app-page-header {
        margin-bottom: 1rem;
      }

      .app-section-heading {
        display: flex;
        align-items: flex-start;
        justify-content: space-between;
        gap: 0.75rem;
        margin-bottom: 0.75rem;
      }

      .app-section-heading h2 {
        margin: 0;
        color: var(--app-text-strong);
        font-size: 1rem;
        font-weight: 700;
        line-height: 1.3;
      }

      .app-section-heading p {
        margin: 0.2rem 0 0;
        color: var(--app-text-muted);
        font-size: 0.78rem;
        line-height: 1.4;
      }

    `,
  ],
})
export class SectionCardComponent {
  @Input() title = '';
  @Input() subtitle = '';
  @Input() styleClass = '';
  @Input() headerType: 'page' | 'section' = 'page';

  get cardClass(): string {
    const custom = this.styleClass.trim();
    return custom ? `app-page-card ${custom}` : 'app-page-card';
  }
}
