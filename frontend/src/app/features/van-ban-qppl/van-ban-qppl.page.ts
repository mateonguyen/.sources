import { AfterViewInit, Component, ElementRef, OnInit, CUSTOM_ELEMENTS_SCHEMA, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { FormsModule, ReactiveFormsModule, FormGroup, FormControl, Validators } from '@angular/forms';

import { SectionCardComponent } from '../../shared/ui/section-card.component';
import { LoadingOverlayComponent } from '../../shared/ui/loading-overlay.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state.component';
import { ConfirmDialogWrapperService } from '../../shared/ui/confirm-dialog-wrapper.service';
import { NotificationService } from '../../core/ui/notification.service';
import { CodesApi, CodeValueDto } from '../codes/codes.api';
import { FilesApi, FileMetadataDto } from '../files/files.api';
import { AuthService } from '../../core/auth/auth.service';
import { VanBanQpplApi, VanBanQpplDto, UpsertVanBanQpplRequest } from './van-ban-qppl.api';

import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { PaginatorModule } from 'primeng/paginator';
import { DropdownModule } from 'primeng/dropdown';
import { TooltipModule } from 'primeng/tooltip';
import { CheckboxModule } from 'primeng/checkbox';
import { DialogModule } from 'primeng/dialog';
import { CalendarModule } from 'primeng/calendar';
import { InputTextareaModule } from 'primeng/inputtextarea';

import { TongHopModeBannerComponent } from '../../shared/ui/tong-hop-mode-banner.component';
interface StatCard {
  label: string;
  value: number;
  icon: string;
  color: string;
}

@Component({
  selector: 'app-van-ban-qppl-page',
  standalone: true,
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  imports: [
    TongHopModeBannerComponent,
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    SectionCardComponent,
    LoadingOverlayComponent,
    EmptyStateComponent,
    TableModule,
    ButtonModule,
    InputTextModule,
    PaginatorModule,
    DropdownModule,
    TooltipModule,
    CheckboxModule,
    DialogModule,
    CalendarModule,
    InputTextareaModule,
  ],
  templateUrl: './van-ban-qppl.page.html',
  styleUrl: './van-ban-qppl.page.scss',
})
export class VanBanQpplPage implements OnInit, AfterViewInit {
  private readonly confirmDialog = inject(ConfirmDialogWrapperService);
  private readonly notificationService = inject(NotificationService);
  private readonly codesApi = inject(CodesApi);
  private readonly filesApi = inject(FilesApi);
  private readonly authService = inject(AuthService);
  private readonly vanBanApi = inject(VanBanQpplApi);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly elementRef = inject(ElementRef);

  stats: StatCard[] = [
    { label: 'TỔNG SỐ VĂN BẢN', value: 0, icon: 'pi pi-copy', color: '#1890ff' },
    { label: 'ĐÃ BAN HÀNH', value: 0, icon: 'pi pi-check-circle', color: '#52c41a' },
    { label: 'ĐANG DỰ THẢO', value: 0, icon: 'pi pi-file-edit', color: '#faad14' },
    { label: 'HẾT HIỆU LỰC', value: 0, icon: 'pi pi-times-circle', color: '#ff4d4f' },
  ];

  allItems: VanBanQpplDto[] = [];
  items: VanBanQpplDto[] = [];
  selectedItems: VanBanQpplDto[] = [];
  first = 0;
  rows = 10;
  totalRecords = 0;
  rowsPerPageOptions = [
    { label: '10', value: 10 },
    { label: '20', value: 20 },
    { label: '50', value: 50 },
  ];
  startIndex = 0;
  endIndex = 0;
  loading = false;

  // Filter state
  searchTerm = '';
  filterLoaiVanBan: string | null = null;
  filterTinhTrang: string | null = null;
  filterCoQuan: string | null = null;
  filterLinhVuc: string | null = null;
  filterNgayBanHanhTu: Date | null = null;
  filterNgayBanHanhDen: Date | null = null;
  advancedVisible = false;
  sortField: string | null = null;
  sortOrder = 1;

  // Dynamic options from CodesApi
  loaiVanBanFilterOptions: { label: string; value: string | null }[] = [];
  loaiVanBanDialogOptions: { label: string; value: string | null }[] = [];
  coQuanFilterOptions: { label: string; value: string | null }[] = [];
  coQuanDialogOptions: { label: string; value: string | null }[] = [];
  linhVucFilterOptions: { label: string; value: string | null }[] = [];
  linhVucDialogOptions: { label: string; value: string | null }[] = [];

  tinhTrangOptions = [
    { label: 'Tất cả', value: null },
    { label: 'Đã ban hành', value: 'Đã ban hành' },
    { label: 'Đang dự thảo', value: 'Đang dự thảo' },
    { label: 'Hết hiệu lực', value: 'Hết hiệu lực' },
  ];

  tinhTrangDialogOptions = [
    { label: 'Đã ban hành', value: 'Đã ban hành' },
    { label: 'Đang dự thảo', value: 'Đang dự thảo' },
    { label: 'Hết hiệu lực', value: 'Hết hiệu lực' },
  ];

  // Dialog
  dialogVisible = false;
  dialogMode: 'create' | 'edit' = 'create';
  editingItem: VanBanQpplDto | null = null;

  // File attachments
  pendingFiles: File[] = [];
  existingFiles: FileMetadataDto[] = [];

  // PDF preview
  previewVisible = false;
  previewTitle = '';
  previewUrl = '';

  dialogForm = new FormGroup({
    soHieu: new FormControl('', Validators.required),
    loaiVanBan: new FormControl<string | null>(null, Validators.required),
    tenVanBan: new FormControl('', Validators.required),
    coQuan: new FormControl(''),
    linhVuc: new FormControl(''),
    ngayBanHanh: new FormControl<Date | null>(null),
    ngayHieuLuc: new FormControl<Date | null>(null),
    tinhTrang: new FormControl<string | null>('Đang dự thảo'),
    trichYeu: new FormControl(''),
  });

  ngOnInit(): void {
    this.initialize();
  }

  ngAfterViewInit(): void {
    this.disableDropdownAutofill();
  }

  private disableDropdownAutofill(): void {
    setTimeout(() => {
      const inputs = this.elementRef.nativeElement.querySelectorAll(
        '.van-ban-dialog .p-dropdown input',
      );
      inputs.forEach((el: HTMLInputElement) => {
        el.setAttribute('autocomplete', 'off');
        el.setAttribute('name', 'no-autofill-' + Math.random().toString(36).slice(2));
        el.setAttribute('data-lpignore', 'true');
        el.setAttribute('data-1p-ignore', 'true');
      });
    });
  }

  async initialize(): Promise<void> {
    this.loading = true;
    try {
      const [loaiVanBan, coQuan, linhVuc] = await Promise.all([
        this.codesApi.getByCodeSafe('LOAI_VAN_BAN'),
        this.codesApi.getByCodeSafe('CO_QUAN_BAN_HANH'),
        this.codesApi.getByCodeSafe('LINH_VUC_VAN_BAN'),
      ]);
      this.loaiVanBanFilterOptions = loaiVanBan
        ? this.toCodeOptions(loaiVanBan.values, 'Tất cả loại văn bản')
        : [{ label: 'Tất cả', value: null }];
      this.loaiVanBanDialogOptions = loaiVanBan
        ? this.toDialogCodeOptions(loaiVanBan.values)
        : [];
      this.coQuanFilterOptions = coQuan
        ? this.toCodeOptions(coQuan.values, 'Tất cả cơ quan')
        : [{ label: 'Tất cả', value: null }];
      this.coQuanDialogOptions = coQuan
        ? this.toDialogCodeOptions(coQuan.values)
        : [];
      this.linhVucFilterOptions = linhVuc
        ? this.toCodeOptions(linhVuc.values, 'Tất cả lĩnh vực')
        : [{ label: 'Tất cả', value: null }];
      this.linhVucDialogOptions = linhVuc
        ? this.toDialogCodeOptions(linhVuc.values)
        : [];
      await this.loadData();
    } catch {
      this.loading = false;
    }
  }

  private toCodeOptions(values: CodeValueDto[], allLabel: string) {
    return [
      { label: allLabel, value: null },
      ...values.filter((v) => v.isActive).map((v) => ({ label: v.name, value: v.value })),
    ];
  }

  private toDialogCodeOptions(values: CodeValueDto[]) {
    return values.filter((v) => v.isActive).map((v) => ({ label: v.name, value: v.value }));
  }

  async loadData(): Promise<void> {
    this.loading = true;
    try {
      const donViId = this.authService.profile()?.donViId ?? undefined;
      this.allItems = await this.vanBanApi.getAll(donViId);
      this.applyClientFilter();
      this.updateStats();
    } catch {
      this.notificationService.show('error', 'Không thể tải dữ liệu văn bản');
    } finally {
      this.loading = false;
    }
  }

  applyClientFilter(): void {
    let filtered = this.allItems;

    if (this.searchTerm?.trim()) {
      const kw = this.searchTerm.toLowerCase();
      filtered = filtered.filter(
        (v) =>
          v.soHieu?.toLowerCase().includes(kw) ||
          v.tenVanBan?.toLowerCase().includes(kw) ||
          v.linhVuc?.toLowerCase().includes(kw),
      );
    }
    if (this.filterLoaiVanBan) {
      filtered = filtered.filter((v) => v.loaiVanBan === this.filterLoaiVanBan);
    }
    if (this.filterTinhTrang) {
      filtered = filtered.filter((v) => v.tinhTrangTrienKhai === this.filterTinhTrang);
    }
    if (this.filterCoQuan) {
      filtered = filtered.filter((v) => v.coQuanBanHanh === this.filterCoQuan);
    }
    if (this.filterLinhVuc) {
      filtered = filtered.filter((v) => v.linhVuc === this.filterLinhVuc);
    }
    if (this.filterNgayBanHanhTu) {
      const from = this.filterNgayBanHanhTu.getTime();
      filtered = filtered.filter((v) => v.ngayBanHanh && new Date(v.ngayBanHanh).getTime() >= from);
    }
    if (this.filterNgayBanHanhDen) {
      const to = this.filterNgayBanHanhDen.getTime();
      filtered = filtered.filter((v) => v.ngayBanHanh && new Date(v.ngayBanHanh).getTime() <= to);
    }

    if (this.sortField) {
      const field = this.sortField;
      const order = this.sortOrder;
      filtered = [...filtered].sort((a, b) => {
        const av = (a as any)[field] ?? '';
        const bv = (b as any)[field] ?? '';
        if (av < bv) return -1 * order;
        if (av > bv) return 1 * order;
        return 0;
      });
    }

    this.totalRecords = filtered.length;
    this.items = filtered.slice(this.first, this.first + this.rows);
    this.startIndex = this.totalRecords > 0 ? this.first + 1 : 0;
    this.endIndex = Math.min(this.first + this.rows, this.totalRecords);
  }

  updateStats(): void {
    const total = this.allItems.length;
    const daBanHanh = this.allItems.filter((v) => v.tinhTrangTrienKhai === 'Đã ban hành').length;
    const dangDuThao = this.allItems.filter((v) => v.tinhTrangTrienKhai === 'Đang dự thảo').length;
    const hetHieuLuc = this.allItems.filter((v) => v.tinhTrangTrienKhai === 'Hết hiệu lực').length;
    this.stats = [
      { label: 'TỔNG SỐ VĂN BẢN', value: total, icon: 'pi pi-copy', color: '#1890ff' },
      { label: 'ĐÃ BAN HÀNH', value: daBanHanh, icon: 'pi pi-check-circle', color: '#52c41a' },
      { label: 'ĐANG DỰ THẢO', value: dangDuThao, icon: 'pi pi-file-edit', color: '#faad14' },
      { label: 'HẾT HIỆU LỰC', value: hetHieuLuc, icon: 'pi pi-times-circle', color: '#ff4d4f' },
    ];
  }

  onPageChange(event: any): void {
    this.first = event.first;
    this.rows = event.rows;
    this.applyClientFilter();
  }

  onRowsChange(newRows: number): void {
    this.rows = newRows;
    this.first = 0;
    this.applyClientFilter();
  }

  onSort(event: { field: string; order: number }): void {
    this.sortField = event.field;
    this.sortOrder = event.order;
    this.applyClientFilter();
  }

  toggleAdvanced(): void {
    this.advancedVisible = !this.advancedVisible;
  }

  applyFilters(): void {
    this.first = 0;
    this.applyClientFilter();
  }

  clearFilters(): void {
    this.searchTerm = '';
    this.filterLoaiVanBan = null;
    this.filterTinhTrang = null;
    this.filterCoQuan = null;
    this.filterLinhVuc = null;
    this.filterNgayBanHanhTu = null;
    this.filterNgayBanHanhDen = null;
    this.advancedVisible = false;
    this.first = 0;
    this.applyClientFilter();
  }

  openCreateDialog(): void {
    this.dialogMode = 'create';
    this.editingItem = null;
    this.dialogForm.reset({ tinhTrang: 'Đang dự thảo' });
    this.dialogForm.markAsPristine();
    this.dialogForm.markAsUntouched();
    Object.values(this.dialogForm.controls).forEach((c) => {
      c.markAsPristine();
      c.markAsUntouched();
    });
    this.pendingFiles = [];
    this.existingFiles = [];
    this.dialogVisible = true;
    this.disableDropdownAutofill();
  }

  async openEditDialog(item: VanBanQpplDto): Promise<void> {
    this.dialogMode = 'edit';
    this.editingItem = item;
    const parseDate = (s: string | null) => (s ? new Date(s) : null);
    this.dialogForm.patchValue({
      soHieu: item.soHieu,
      tenVanBan: item.tenVanBan,
      loaiVanBan: item.loaiVanBan,
      coQuan: item.coQuanBanHanh,
      linhVuc: item.linhVuc,
      ngayBanHanh: parseDate(item.ngayBanHanh),
      ngayHieuLuc: parseDate(item.ngayHieuLuc ?? null),
      tinhTrang: item.tinhTrangTrienKhai,
      trichYeu: item.trichYeu,
    });
    this.dialogForm.markAsPristine();
    this.dialogForm.markAsUntouched();
    Object.values(this.dialogForm.controls).forEach((c) => {
      c.markAsPristine();
      c.markAsUntouched();
    });
    this.pendingFiles = [];
    this.existingFiles = item.fileDinhKems ?? [];
    this.dialogVisible = true;
    this.disableDropdownAutofill();
  }

  async saveDialog(): Promise<void> {
    if (this.dialogForm.invalid) {
      this.dialogForm.markAllAsTouched();
      return;
    }
    const val = this.dialogForm.getRawValue();
    const donViId = this.authService.profile()?.donViId ?? 0;

    const toIsoDate = (d: Date | null): string =>
      d
        ? `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
        : '';

    const request: UpsertVanBanQpplRequest = {
      donViId,
      soHieu: val.soHieu ?? '',
      tenVanBan: val.tenVanBan || null,
      loaiVanBan: val.loaiVanBan || null,
      coQuanBanHanh: val.coQuan || null,
      ngayBanHanh: toIsoDate(val.ngayBanHanh as Date | null) || new Date().toISOString().slice(0, 10),
      ngayHieuLuc: toIsoDate(val.ngayHieuLuc as Date | null) || null,
      linhVuc: val.linhVuc || null,
      trichYeu: val.trichYeu || null,
      tinhTrangTrienKhai: val.tinhTrang || null,
      ghiChu: null,
    };

    this.loading = true;
    try {
      let saved: VanBanQpplDto;
      if (this.dialogMode === 'create') {
        saved = await this.vanBanApi.create(request);
        this.notificationService.show('success', 'Thêm văn bản thành công');
      } else {
        saved = await this.vanBanApi.update(this.editingItem!.id, request);
        this.notificationService.show('success', 'Cập nhật văn bản thành công');
      }
      await this.uploadPendingFiles(saved.id);
      this.dialogVisible = false;
      await this.loadData();
    } catch {
      this.notificationService.show('error', 'Lưu thất bại, vui lòng thử lại');
    } finally {
      this.loading = false;
    }
  }

  async onDelete(item: VanBanQpplDto): Promise<void> {
    const confirmed = await this.confirmDialog.confirmDelete({
      header: 'Xác nhận xóa văn bản',
      message: `Bạn có chắc chắn muốn xóa văn bản "${item.soHieu}"?`,
    });
    if (!confirmed) return;
    try {
      await this.vanBanApi.delete(item.id);
      this.notificationService.show('success', 'Đã xóa văn bản thành công');
      await this.loadData();
    } catch {
      this.notificationService.show('error', 'Xóa thất bại');
    }
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (!input.files) return;
    for (const file of Array.from(input.files)) {
      this.pendingFiles.push(file);
    }
    input.value = '';
  }

  removePendingFile(index: number): void {
    this.pendingFiles.splice(index, 1);
  }

  async removeExistingFile(file: FileMetadataDto): Promise<void> {
    const confirmed = await this.confirmDialog.confirmDelete({
      header: 'Xác nhận xóa tệp',
      message: `Bạn có chắc chắn muốn xóa tệp "${file.fileName}"?`,
    });
    if (!confirmed) return;
    await this.filesApi.delete(file.id);
    this.existingFiles = this.existingFiles.filter((f) => f.id !== file.id);
  }

  async uploadPendingFiles(entityId: number): Promise<void> {
    if (this.pendingFiles.length === 0) return;
    const donViId = this.authService.profile()?.donViId ?? 0;
    await Promise.all(
      this.pendingFiles.map((f) =>
        this.filesApi.upload('VanBanQppl', entityId, donViId, f).catch(() => null),
      ),
    );
    this.pendingFiles = [];
  }

  formatFileSize(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }

  getFileIcon(mimeType: string): string {
    if (mimeType === 'application/pdf') return 'pi pi-file-pdf';
    if (mimeType.includes('word') || mimeType.includes('msword')) return 'pi pi-file-word';
    if (mimeType.includes('excel') || mimeType.includes('spreadsheet')) return 'pi pi-file-excel';
    return 'pi pi-file';
  }

  isPdf(mimeType: string): boolean {
    return mimeType === 'application/pdf';
  }

  async downloadFile(file: FileMetadataDto): Promise<void> {
    try {
      const url = await this.filesApi.getDownloadUrl(file.id);
      const a = document.createElement('a');
      a.href = url;
      a.download = file.fileName;
      a.target = '_blank';
      a.rel = 'noopener';
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
    } catch {
      this.notificationService.show('error', 'Không thể tải tệp, vui lòng thử lại');
    }
  }

  async openPreview(file: FileMetadataDto): Promise<void> {
    try {
      const url = await this.filesApi.getDownloadUrl(file.id);
      this.previewTitle = file.fileName;
      this.previewUrl = url;
      this.previewVisible = true;
    } catch {
      this.notificationService.show('error', 'Không thể mở xem trước');
    }
  }

  closePreview(): void {
    this.previewVisible = false;
    this.previewUrl = '';
    this.previewTitle = '';
  }

  get safePreviewUrl(): SafeResourceUrl {
    return this.sanitizer.bypassSecurityTrustResourceUrl(this.previewUrl);
  }

  isInvalid(controlName: string): boolean {
    const control = this.dialogForm.get(controlName);
    return !!control && control.invalid && (control.touched || control.dirty);
  }

  getError(controlName: string): string {
    const control = this.dialogForm.get(controlName);
    if (!control?.errors) return '';

    if (control.errors['required']) {
      switch (controlName) {
        case 'soHieu': return 'Vui lòng nhập số hiệu';
        case 'loaiVanBan': return 'Vui lòng chọn loại văn bản';
        case 'tenVanBan': return 'Vui lòng nhập tên văn bản';
        default: return 'Trường này là bắt buộc';
      }
    }

    return 'Dữ liệu không hợp lệ';
  }

  getStatusClass(tinhTrang: string): string {
    switch (tinhTrang) {
      case 'Đã ban hành': return 'status-approved';
      case 'Đang dự thảo': return 'status-draft';
      case 'Hết hiệu lực': return 'status-expired';
      default: return '';
    }
  }
}
