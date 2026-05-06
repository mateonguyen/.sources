import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DropdownModule } from 'primeng/dropdown';
import { InputSwitchModule } from 'primeng/inputswitch';
import { SectionCardComponent } from '../../shared/ui/section-card.component';
import { NotificationService } from '../../core/ui/notification.service';
import { UserSettingsService, UserSettings } from './user-settings.service';

@Component({
  selector: 'app-system-settings-page',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    SectionCardComponent,
    ButtonModule,
    DropdownModule,
    InputSwitchModule,
  ],
  templateUrl: './system-settings.page.html',
  styleUrl: './system-settings.page.scss',
})
export class SystemSettingsPage {
  form: UserSettings;

  readonly pageSizeOptions = [
    { label: '10 dòng', value: 10 as 10 | 20 | 50 },
    { label: '20 dòng', value: 20 as 10 | 20 | 50 },
    { label: '50 dòng', value: 50 as 10 | 20 | 50 },
  ];

  constructor(
    private readonly settingsService: UserSettingsService,
    private readonly notificationService: NotificationService,
  ) {
    this.form = this.settingsService.get();
  }

  save(): void {
    this.settingsService.save({ ...this.form });
    this.notificationService.show(
      'success',
      'Cấu hình đã được lưu thành công.',
    );
  }

  restoreDefaults(): void {
    this.settingsService.reset();
    this.form = this.settingsService.get();
    this.notificationService.show('info', 'Đã khôi phục về cấu hình mặc định.');
  }
}
