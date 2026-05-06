import { CommonModule } from '@angular/common';
import { Component, ViewChild } from '@angular/core';
import { FormsModule, NgForm } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { SectionCardComponent } from '../../shared/ui/section-card.component';
import { NotificationService } from '../../core/ui/notification.service';
import { AccountService, AccountDetail } from './account.service';
import { DonViApi } from '../don-vi/don-vi.api';

interface ProfileForm {
  username: string;
  hoTen: string;
  email: string;
  soDienThoai: string;
  donViTen: string | null;
  roles: string[];
}

@Component({
  selector: 'app-account-info-page',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    SectionCardComponent,
    InputTextModule,
    ButtonModule,
  ],
  templateUrl: './account-info.page.html',
  styleUrl: './account-info.page.scss',
})
export class AccountInfoPage {
  @ViewChild('profileForm') profileForm!: NgForm;

  loading = true;
  saving = false;
  submitted = false;
  apiError = '';

  form: ProfileForm = {
    username: '',
    hoTen: '',
    email: '',
    soDienThoai: '',
    donViTen: null,
    roles: [],
  };

  private original: Omit<ProfileForm, 'username' | 'donViTen' | 'roles'> = {
    hoTen: '',
    email: '',
    soDienThoai: '',
  };

  constructor(
    private readonly accountService: AccountService,
    private readonly notificationService: NotificationService,
    private readonly donViApi: DonViApi,
  ) {
    this.load();
  }

  async load(): Promise<void> {
    this.loading = true;
    this.apiError = '';
    try {
      const data: AccountDetail = await this.accountService.getProfile();
      this.applyProfile(data);
    } catch {
      this.apiError = 'Không thể tải thông tin tài khoản. Vui lòng thử lại.';
    } finally {
      this.loading = false;
    }
  }

  async save(): Promise<void> {
    this.submitted = true;

    if (this.profileForm.invalid) {
      return;
    }

    this.saving = true;
    this.apiError = '';
    try {
      await this.accountService.updateProfile({
        hoTen: this.form.hoTen.trim(),
        email: this.form.email.trim() || null,
        soDienThoai: this.form.soDienThoai.trim() || null,
      });
      this.original = {
        hoTen: this.form.hoTen,
        email: this.form.email,
        soDienThoai: this.form.soDienThoai,
      };
      this.submitted = false;
      this.notificationService.show(
        'success',
        'Cập nhật thông tin tài khoản thành công.',
      );
    } catch {
      this.apiError = 'Không thể lưu thông tin. Vui lòng thử lại.';
    } finally {
      this.saving = false;
    }
  }

  reset(): void {
    this.submitted = false;
    this.form.hoTen = this.original.hoTen;
    this.form.email = this.original.email;
    this.form.soDienThoai = this.original.soDienThoai;
    this.profileForm?.resetForm({
      hoTen: this.original.hoTen,
      email: this.original.email,
      soDienThoai: this.original.soDienThoai,
    });
  }

  private applyProfile(data: AccountDetail): void {
    this.form.username = data.username;
    this.form.hoTen = data.hoTen ?? '';
    this.form.email = data.email ?? '';
    this.form.soDienThoai = data.soDienThoai ?? '';
    this.form.donViTen = data.donViTen;
    this.form.roles = data.roles ?? [];
    if (!this.form.donViTen && data.donViId) {
      void this.resolveDonViTen(data.donViId);
    }
    this.original = {
      hoTen: this.form.hoTen,
      email: this.form.email,
      soDienThoai: this.form.soDienThoai,
    };
  }

  private async resolveDonViTen(donViId: number): Promise<void> {
    try {
      const donVi = await this.donViApi.getById(donViId);
      this.form.donViTen = `${donVi.maDonVi} - ${donVi.tenDonVi}`;
    } catch {
      this.form.donViTen = null;
    }
  }
}
