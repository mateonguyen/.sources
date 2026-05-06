import { Component, Input } from '@angular/core';
import { TagModule } from 'primeng/tag';

@Component({
  selector: 'app-status-badge',
  standalone: true,
  imports: [TagModule],
  template: `
    <p-tag
      styleClass="app-status-tag"
      [value]="label"
      [severity]="severity"
    ></p-tag>
  `,
})
export class StatusBadgeComponent {
  @Input({ required: true }) label = '';
  @Input() status = '';
  @Input() tone: 'neutral' | 'info' | 'success' | 'warning' | 'danger' =
    'neutral';

  get severity(): 'contrast' | 'info' | 'success' | 'warning' | 'danger' {
    if (this.status) {
      return this.severityByStatus(this.status);
    }

    if (this.tone === 'neutral') {
      return 'contrast';
    }

    return this.tone;
  }

  private severityByStatus(
    status: string,
  ): 'contrast' | 'info' | 'success' | 'warning' | 'danger' {
    const normalized = status.trim().toLowerCase();

    if (normalized === 'draft') {
      return 'warning';
    }
    if (normalized === 'submitted') {
      return 'info';
    }
    if (normalized === 'locked') {
      return 'success';
    }
    if (normalized === 'error' || normalized === 'rejected') {
      return 'danger';
    }

    return 'contrast';
  }
}
