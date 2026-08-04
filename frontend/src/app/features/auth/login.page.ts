import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { CheckboxModule } from 'primeng/checkbox';
import { AuthService } from '../../core/auth/auth.service';
import { SectionCardComponent } from '../../shared/ui/section-card.component';

@Component({
  selector: 'app-login-page',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    SectionCardComponent,
    InputTextModule,
    PasswordModule,
    CheckboxModule,
    ButtonModule,
  ],
  templateUrl: './login.page.html',
  styleUrl: './login.page.scss',
})
export class LoginPage {
  error = '';
  loading = false;
  showMfaWarning = false;

  readonly form = this.formBuilder.group({
    username: ['admin', [Validators.required]],
    password: ['Admin@123', [Validators.required]],
    rememberMe: [false],
  });

  constructor(
    private readonly formBuilder: FormBuilder,
    private readonly authService: AuthService,
    private readonly router: Router,
  ) {}

  async submit(): Promise<void> {
    if (this.form.invalid || this.loading) {
      return;
    }

    this.error = '';
    this.showMfaWarning = false;
    this.loading = true;

    try {
      const value = this.form.getRawValue();
      const rememberMe = value.rememberMe ?? false;

      // Level-3: Kiểm tra MFA nếu rememberMe được tick
      if (rememberMe) {
        const isNewDevice = await this.isNewDevice();
        if (isNewDevice) {
          this.showMfaWarning = true;
          // TODO: Trong production, show MFA dialog hoặc step-up auth
          // Ở đây chỉ hiển thị warning
          console.warn(
            'New device detected. MFA verification may be required.',
          );
        }
      }

      // Gọi login với rememberMe flag
      await this.authService.login(
        value.username ?? '',
        value.password ?? '',
        rememberMe,
      );
      await this.router.navigate(['/trang-chu'], { replaceUrl: true });
    } catch (error: any) {
      this.error =
        error?.error?.message ||
        'Đăng nhập thất bại. Vui lòng kiểm tra thông tin.';
    } finally {
      this.loading = false;
    }
  }

  /**
   * Kiểm tra xem thiết bị này là thiết bị mới (chưa đăng nhập bao giờ).
   * Level-3: Dùng để quyết định có yêu cầu MFA hay không.
   */
  private async isNewDevice(): Promise<boolean> {
    try {
      // Lấy danh sách phiên hiện tại
      const sessions = await this.authService.getSessions();
      // Nếu danh sách phiên rỗng, đây là thiết bị mới
      return sessions.length === 0;
    } catch {
      // Nếu không lấy được danh sách (user chưa đăng nhập), coi như thiết bị mới
      return true;
    }
  }
}
