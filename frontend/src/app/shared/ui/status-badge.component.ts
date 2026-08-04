import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-status-badge',
  standalone: true,
  imports: [CommonModule],
  template: `<span [class]="cssClass">{{ label }}</span>`,
})
export class StatusBadgeComponent {
  @Input({ required: true }) label = '';
  @Input() status = '';
  @Input() tone: 'neutral' | 'info' | 'success' | 'warning' | 'danger' = 'neutral';

  get cssClass(): string {
    const modifier = this.status
      ? this.modifierByStatus(this.status)
      : this.modifierByTone(this.tone);
    return `status-badge ${modifier}`;
  }

  private modifierByTone(tone: string): string {
    const map: Record<string, string> = {
      neutral: 'status-badge--da-dong',
      info:    'status-badge--chuan-bi',
      success: 'status-badge--dang-mo',
      warning: 'status-badge--draft',
      danger:  'status-badge--qua-han',
    };
    return map[tone] ?? 'status-badge--da-dong';
  }

  private modifierByStatus(status: string): string {
    const map: Record<string, string> = {
      draft:     'status-badge--draft',
      submitted: 'status-badge--xac-nhan',
      locked:    'status-badge--khoa',
      error:     'status-badge--qua-han',
      rejected:  'status-badge--qua-han',
    };
    return map[status.trim().toLowerCase()] ?? 'status-badge--da-dong';
  }
}
