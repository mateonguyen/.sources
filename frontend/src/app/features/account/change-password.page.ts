import { CommonModule } from '@angular/common';
import { Component, ViewChild } from '@angular/core';
import { FormsModule, NgForm } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { SectionCardComponent } from '../../shared/ui/section-card.component';
import { NotificationService } from '../../core/ui/notification.service';
import { AccountService } from './account.service';

interface ChangePasswordForm {
  currentPassword: string;
  newPassword: string;
  confirmPassword: string;
}

@Component({
  selector: 'app-change-password-page',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    SectionCardComponent,
    InputTextModule,
    ButtonModule,
  ],
  templateUrl: './change-password.page.html',
  styleUrl: './change-password.page.scss',
})
export class ChangePasswordPage {
  @ViewChild('cpForm') cpForm!: NgForm;

  saving = false;
  submitted = false;
  apiError = '';

  showCurrent = false;
  showNew = false;
  showConfirm = false;

  form: ChangePasswordForm = {
    currentPassword: '',
    newPassword: '',
    confirmPassword: '',
  };

  get confirmMismatch(): boolean {
    return (
      !!this.form.newPassword &&
      !!this.form.confirmPassword &&
      this.form.newPassword !== this.form.confirmPassword
    );
  }

  constructor(
    private readonly accountService: AccountService,
    private readonly notificationService: NotificationService,
  ) {}

  async submit(): Promise<void> {
    this.submitted = true;

    if (this.cpForm.invalid || this.confirmMismatch) {
      return;
    }

    this.saving = true;
    this.apiError = '';
    try {
      await this.accountService.changePassword({
        matKhauHienTai: this.form.currentPassword,
        matKhauMoi: this.form.newPassword,
      });
      this.notificationService.show(
        'success',
        'Đổi mật khẩu thành công. Vui lòng đăng nhập lại nếu cần.',
      );
      this.resetForm();
    } catch (err: unknown) {
      const msg = (err as { error?: { Error?: { Message?: string } } })?.error
        ?.Error?.Message;
      this.apiError =
        msg ??
        'Không thể đổi mật khẩu. Vui lòng kiểm tra lại mật khẩu hiện tại.';
    } finally {
      this.saving = false;
    }
  }

  resetForm(): void {
    this.submitted = false;
    this.apiError = '';
    this.form = { currentPassword: '', newPassword: '', confirmPassword: '' };
    this.cpForm?.resetForm();
  }
}
