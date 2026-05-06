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
      <app-page-header *ngIf="title" [title]="title" [subtitle]="subtitle">
        <ng-content select="[sectionActions]"></ng-content>
      </app-page-header>
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
    `,
  ],
})
export class SectionCardComponent {
  @Input() title = '';
  @Input() subtitle = '';
  @Input() styleClass = '';

  get cardClass(): string {
    const custom = this.styleClass.trim();
    return custom ? `app-page-card ${custom}` : 'app-page-card';
  }
}
