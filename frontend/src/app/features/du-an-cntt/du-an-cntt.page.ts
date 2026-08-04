import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  FormsModule,
  ReactiveFormsModule,
  FormBuilder,
  FormGroup,
  Validators,
} from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { DropdownModule } from 'primeng/dropdown';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { InputTextareaModule } from 'primeng/inputtextarea';
import { PaginatorModule } from 'primeng/paginator';
import { TableModule } from 'primeng/table';
import { TooltipModule } from 'primeng/tooltip';
import { DuAnCnttApi, DuAnCnttDto, UpsertDuAnCnttRequest } from './du-an-cntt.api';
import { CodesApi } from '../codes/codes.api';
import { AuthService } from '../../core/auth/auth.service';
import { NotificationService } from '../../core/ui/notification.service';
import { ConfirmDialogWrapperService } from '../../shared/ui/confirm-dialog-wrapper.service';
import { SectionCardComponent } from '../../shared/ui/section-card.component';
import { LoadingOverlayComponent } from '../../shared/ui/loading-overlay.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state.component';

import { TongHopModeBannerComponent } from '../../shared/ui/tong-hop-mode-banner.component';
@Component({
  selector: 'app-du-an-cntt',
  standalone: true,
  imports: [
    TongHopModeBannerComponent,
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    SectionCardComponent,
    LoadingOverlayComponent,
    EmptyStateComponent,
    ButtonModule,
    DialogModule,
    DropdownModule,
    InputNumberModule,
    InputTextModule,
    InputTextareaModule,
    PaginatorModule,
    TableModule,
    TooltipModule,
  ],
  templateUrl: './du-an-cntt.page.html',
  styleUrls: ['./du-an-cntt.page.scss'],
})
export class DuAnCnttPage implements OnInit {
  private readonly api = inject(DuAnCnttApi);
  private readonly codesApi = inject(CodesApi);
  private readonly authService = inject(AuthService);
  private readonly notification = inject(NotificationService);
  private readonly confirmDialog = inject(ConfirmDialogWrapperService);
  private readonly fb = inject(FormBuilder);

  // Signals
  items = signal<DuAnCnttDto[]>([]);
  loading = signal(false);
  showDialog = signal(false);
  isEdit = signal(false);
  donViId = signal(0);

  // Form
  form!: FormGroup;
  selectedItem: DuAnCnttDto | null = null;

  // Danh mục codes
  nguonVonList = signal<any[]>([]);

  // Filter state
  searchTerm = '';
  filterNguonVon: string | null = null;

  // Pagination
  first = 0;
  rows = 10;
  readonly pageSizeOptions = [
    { label: '10', value: 10 },
    { label: '20', value: 20 },
    { label: '50', value: 50 },
  ];

  get filteredItems(): DuAnCnttDto[] {
    let result = this.items();

    if (this.searchTerm?.trim()) {
      const q = this.searchTerm.toLowerCase();
      result = result.filter(
        (i) =>
          i.tenDuAn?.toLowerCase().includes(q) ||
          i.donViChuTri?.toLowerCase().includes(q),
      );
    }

    if (this.filterNguonVon) {
      result = result.filter((i) => i.nguonVon === this.filterNguonVon);
    }

    return result;
  }

  get paginatedItems(): DuAnCnttDto[] {
    return this.filteredItems.slice(this.first, this.first + this.rows);
  }

  get totalRecords(): number {
    return this.filteredItems.length;
  }

  get displayEnd(): number {
    return Math.min(this.first + this.rows, this.totalRecords);
  }

  get stats() {
    const all = this.items();
    const tongKinhPhi = all.reduce((s, i) => s + (i.tongKinhPhi ?? 0), 0);
    return [
      { label: 'Tổng dự án', value: all.length, icon: 'pi-folder-open', color: 'var(--blue-500)' },
      {
        label: 'Tổng kinh phí (tỷ VNĐ)',
        value: tongKinhPhi.toLocaleString('vi-VN', { maximumFractionDigits: 1 }),
        icon: 'pi-chart-bar',
        color: 'var(--green-500)',
      },
      {
        label: 'Ngân sách nhà nước',
        value: all.filter((i) => i.nguonVon === 'NGAN_SACH_NHA_NUOC').length,
        icon: 'pi-building',
        color: 'var(--orange-500)',
      },
      {
        label: 'Nguồn khác',
        value: all.filter((i) => i.nguonVon && i.nguonVon !== 'NGAN_SACH_NHA_NUOC').length,
        icon: 'pi-wallet',
        color: 'var(--purple-500)',
      },
    ];
  }

  get nguonVonFilterOptions() {
    return [
      { label: 'Tất cả nguồn vốn', value: null },
      ...this.nguonVonList().map((v) => ({ label: v.name, value: v.value })),
    ];
  }

  getNguonVonLabel(value: string | undefined): string {
    if (!value) return '-';
    return this.nguonVonList().find((v) => v.value === value)?.name ?? value;
  }

  onPageChange(event: { first?: number; rows?: number }): void {
    this.first = event.first ?? this.first;
    this.rows = event.rows ?? this.rows;
  }

  clearFilters(): void {
    this.searchTerm = '';
    this.filterNguonVon = null;
    this.first = 0;
  }

  ngOnInit() {
    this.initForm();
    void this.initialize();
  }

  private async initialize() {
    const user = this.authService.profile();
    if (user) {
      this.donViId.set(user.donViId);
    }

    await this.loadCodes();
    this.load();
  }

  private initForm() {
    this.form = this.fb.group({
      tenDuAn: ['', Validators.required],
      donViChuTri: [''],
      namTrienKhai: [null],
      namDuaVaoSuDung: [null],
      tongKinhPhi: [0],
      nguonVon: [''],
      ghiChu: [''],
    });
  }

  private async loadCodes() {
    try {
      const nguonVon = await this.codesApi.getByCode('NGUON_VON_DU_AN').catch(() => null);
      this.nguonVonList.set(nguonVon?.values ?? []);
    } catch {
      this.notification.show('warning', 'Không tải được danh mục dùng chung');
    }
  }

  load() {
    this.loading.set(true);
    this.api
      .getAll({ donViId: this.donViId() })
      .then((data) => {
        this.items.set(data);
        this.first = 0;
      })
      .catch(() => {
        this.notification.show('error', 'Lỗi tải dữ liệu dự án đầu tư');
      })
      .finally(() => {
        this.loading.set(false);
      });
  }

  openCreate() {
    this.isEdit.set(false);
    this.selectedItem = null;
    this.form.reset();
    this.form.markAsPristine();
    this.form.markAsUntouched();
    Object.values(this.form.controls).forEach((c) => {
      c.markAsPristine();
      c.markAsUntouched();
    });
    this.showDialog.set(true);
  }

  select(item: DuAnCnttDto) {
    this.isEdit.set(true);
    this.selectedItem = item;
    this.form.patchValue({
      tenDuAn: item.tenDuAn,
      donViChuTri: item.donViChuTri,
      namTrienKhai: item.namTrienKhai,
      namDuaVaoSuDung: item.namDuaVaoSuDung,
      tongKinhPhi: item.tongKinhPhi,
      nguonVon: item.nguonVon,
      ghiChu: item.ghiChu,
    });
    this.form.markAsPristine();
    this.form.markAsUntouched();
    Object.values(this.form.controls).forEach((c) => {
      c.markAsPristine();
      c.markAsUntouched();
    });
    this.showDialog.set(true);
  }

  save() {
    if (!this.form.valid) {
      this.form.markAllAsTouched();
      this.notification.show('warning', 'Vui lòng kiểm tra lại dữ liệu');
      return;
    }

    const payload: UpsertDuAnCnttRequest = {
      ...this.form.value,
      donViId: this.donViId(),
    };

    this.loading.set(true);
    const request =
      this.isEdit() && this.selectedItem
        ? this.api.update(this.selectedItem.id!, payload)
        : this.api.create(payload);

    request
      .then(() => {
        this.notification.show(
          'success',
          this.isEdit() ? 'Cập nhật dự án thành công' : 'Thêm dự án thành công',
        );
        this.closeDialog();
        this.load();
      })
      .catch(() => {
        this.notification.show('error', 'Lỗi lưu dữ liệu');
        this.loading.set(false);
      });
  }

  async remove(item: DuAnCnttDto) {
    const confirmed = await this.confirmDialog.confirmDelete({
      message: 'Xóa dự án này?',
      acceptLabel: 'Xóa',
      rejectLabel: 'Hủy',
    });

    if (!confirmed) {
      return;
    }

    this.loading.set(true);
    this.api
      .delete(item.id!)
      .then(() => {
        this.notification.show('success', 'Xóa dự án thành công');
        this.load();
      })
      .catch(() => {
        this.notification.show('error', 'Lỗi xóa dữ liệu');
        this.loading.set(false);
      });
  }

  onDialogVisibleChange(visible: boolean) {
    if (!visible) {
      this.closeDialog();
      return;
    }

    this.showDialog.set(true);
  }

  isInvalid(controlName: string): boolean {
    const control = this.form.get(controlName);
    return !!control && control.invalid && (control.touched || control.dirty);
  }

  getError(controlName: string): string {
    const control = this.form.get(controlName);
    if (!control?.errors) return '';
    if (control.errors['required']) {
      if (controlName === 'tenDuAn') return 'Vui lòng nhập tên dự án';
      return 'Trường này là bắt buộc';
    }
    return 'Dữ liệu không hợp lệ';
  }

  closeDialog() {
    this.showDialog.set(false);
    this.selectedItem = null;
    this.form.reset();
    this.loading.set(false);
  }
}
