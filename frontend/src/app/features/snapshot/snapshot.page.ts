import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import {
  FormBuilder,
  FormsModule,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { DropdownModule } from 'primeng/dropdown';
import { ButtonModule } from 'primeng/button';
import { InputTextareaModule } from 'primeng/inputtextarea';
import { TableModule } from 'primeng/table';
import { SnapshotApi, SnapshotDto } from './snapshot.api';
import { NotificationService } from '../../core/ui/notification.service';
import { AuthService } from '../../core/auth/auth.service';
import { Router } from '@angular/router';
import { LoadingOverlayComponent } from '../../shared/ui/loading-overlay.component';
import { SectionCardComponent } from '../../shared/ui/section-card.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state.component';
import { StatusBadgeComponent } from '../../shared/ui/status-badge.component';
import { KyBaoCaoApi, KyBaoCaoDto } from '../ky-bao-cao/ky-bao-cao.api';

interface SnapshotPreviewMetric {
  module: string;
  count: number;
}

@Component({
  selector: 'app-snapshot-page',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    SectionCardComponent,
    EmptyStateComponent,
    StatusBadgeComponent,
    ButtonModule,
    DropdownModule,
    InputTextareaModule,
    TableModule,
    LoadingOverlayComponent,
  ],
  templateUrl: './snapshot.page.html',
  styleUrl: './snapshot.page.scss',
})
export class SnapshotPage {
  loading = false;
  working = false;
  status = '';
  snapshot: SnapshotDto | null = null;
  snapshots: SnapshotDto[] = [];
  kyBaoCaos: KyBaoCaoDto[] = [];
  selectedKyBaoCaoId: number | null = null;
  pdfUrl: string | null = null;

  readonly form = this.formBuilder.group({
    snapshotJson: ['', [Validators.required]],
    summaryJson: [''],
    ghiChu: [''],
  });

  constructor(
    private readonly formBuilder: FormBuilder,
    private readonly snapshotApi: SnapshotApi,
    private readonly kyBaoCaoApi: KyBaoCaoApi,
    private readonly authService: AuthService,
    private readonly notificationService: NotificationService,
    private readonly router: Router,
  ) {
    void this.load();
  }

  get currentDonViId(): number {
    return this.authService.profile()?.donViId ?? 0;
  }

  get kyOptions(): Array<{ label: string; value: number }> {
    return this.kyBaoCaos.map((item) => ({
      label: `${item.kyCode} · ${this.kyStatusText(item.trangThai)}`,
      value: item.id,
    }));
  }

  get selectedKyBaoCao(): KyBaoCaoDto | null {
    if (!this.selectedKyBaoCaoId) {
      return null;
    }

    return (
      this.kyBaoCaos.find((item) => item.id === this.selectedKyBaoCaoId) ?? null
    );
  }

  get canCreateDraft(): boolean {
    return (
      !!this.selectedKyBaoCaoId &&
      !!this.currentDonViId &&
      !!this.form.getRawValue().snapshotJson?.trim()
    );
  }

  get canEditDraft(): boolean {
    return !!this.snapshot && this.snapshot.trangThai === 1;
  }

  get previewMetrics(): SnapshotPreviewMetric[] {
    const raw = this.form.getRawValue().snapshotJson?.trim();
    if (!raw) {
      return [];
    }

    try {
      const parsed = JSON.parse(raw) as Record<string, unknown>;
      return Object.entries(parsed)
        .map(([module, value]) => ({
          module,
          count: Array.isArray(value) ? value.length : value ? 1 : 0,
        }))
        .filter((item) => item.count > 0)
        .sort((left, right) => right.count - left.count);
    } catch {
      return [];
    }
  }

  async load(): Promise<void> {
    this.loading = true;
    try {
      const [currentKy, allKy] = await Promise.all([
        this.kyBaoCaoApi.getCurrent().catch(() => null),
        this.kyBaoCaoApi.getAll(),
      ]);

      this.kyBaoCaos = allKy;
      this.selectedKyBaoCaoId = currentKy?.id ?? allKy[0]?.id ?? null;
      if (this.selectedKyBaoCaoId) {
        await this.loadSnapshotsForKy(this.selectedKyBaoCaoId);
      }
    } finally {
      this.loading = false;
    }
  }

  async onKyBaoCaoChange(value: number | null | undefined): Promise<void> {
    const nextValue = value ?? null;
    this.selectedKyBaoCaoId = nextValue;
    this.snapshot = null;
    this.pdfUrl = null;
    this.form.reset({
      snapshotJson: '',
      summaryJson: '',
      ghiChu: '',
    });

    if (!nextValue) {
      this.snapshots = [];
      return;
    }

    await this.loadSnapshotsForKy(nextValue);
  }

  async buildAutoFill(): Promise<void> {
    if (!this.selectedKyBaoCaoId || !this.currentDonViId) {
      return;
    }

    this.working = true;
    try {
      const snapshotJson = await this.snapshotApi.buildSnapshotJson(
        this.selectedKyBaoCaoId,
        this.currentDonViId,
      );

      this.form.patchValue({ snapshotJson });
      this.status = 'Đã tự động gom dữ liệu từ các mô-đun nghiệp vụ.';
      this.notificationService.show(
        'success',
        'Đã nạp dữ liệu auto-fill cho snapshot.',
      );
    } finally {
      this.working = false;
    }
  }

  async createDraft(): Promise<void> {
    if (this.form.invalid || !this.selectedKyBaoCaoId || !this.currentDonViId) {
      return;
    }

    this.working = true;
    try {
      const value = this.form.getRawValue();
      this.snapshot = await this.snapshotApi.createDraft(
        this.selectedKyBaoCaoId,
        this.currentDonViId,
        value.snapshotJson ?? '{}',
        value.summaryJson ?? '',
      );

      this.status = `Đã tạo bản nháp phiên bản ${this.snapshot.phienBan}.`;
      this.notificationService.show(
        'success',
        'Tạo bản nháp snapshot thành công.',
      );
      await this.loadSnapshotsForKy(this.selectedKyBaoCaoId, this.snapshot.id);
    } finally {
      this.working = false;
    }
  }

  async selectSnapshot(snapshot: SnapshotDto): Promise<void> {
    this.working = true;
    try {
      this.snapshot = await this.snapshotApi.getById(snapshot.id);
      this.form.patchValue({
        snapshotJson: this.snapshot.snapshotJson,
        summaryJson: this.snapshot.summaryJson ?? '',
        ghiChu: '',
      });
      this.status = `Đã tải snapshot #${this.snapshot.id}.`;
    } finally {
      this.working = false;
    }
  }

  async updateDraft(): Promise<void> {
    if (!this.snapshot || this.snapshot.trangThai !== 1) {
      this.status = 'Chỉ có thể cập nhật snapshot ở trạng thái Draft.';
      return;
    }

    const value = this.form.getRawValue();
    this.working = true;
    try {
      this.snapshot = await this.snapshotApi.updateDraft(
        this.snapshot.id,
        value.snapshotJson ?? '{}',
        value.summaryJson ?? '',
        value.ghiChu ?? '',
      );
      this.notificationService.show('success', 'Cập nhật bản nháp thành công.');
      this.status = `Đã cập nhật snapshot #${this.snapshot.id}.`;
      if (this.selectedKyBaoCaoId) {
        await this.loadSnapshotsForKy(
          this.selectedKyBaoCaoId,
          this.snapshot.id,
        );
      }
    } finally {
      this.working = false;
    }
  }

  async submit(): Promise<void> {
    if (!this.snapshot) {
      this.status = 'Cần tạo hoặc chọn bản nháp trước khi nộp báo cáo.';
      return;
    }

    if (this.snapshot.trangThai !== 1) {
      this.status = 'Snapshot không ở trạng thái Draft nên không thể nộp.';
      return;
    }

    this.working = true;
    try {
      const value = this.form.getRawValue();
      this.snapshot = await this.snapshotApi.submit(
        this.snapshot.id,
        value.ghiChu ?? 'Nộp từ giao diện snapshot',
      );
      this.notificationService.show('success', 'Nộp snapshot thành công.');
      this.status = `Đã nộp snapshot #${this.snapshot.id}.`;
      if (this.selectedKyBaoCaoId) {
        await this.loadSnapshotsForKy(
          this.selectedKyBaoCaoId,
          this.snapshot.id,
        );
      }
    } finally {
      this.working = false;
    }
  }

  async getPdf(): Promise<void> {
    if (!this.snapshot) {
      return;
    }

    this.working = true;
    try {
      const result = await this.snapshotApi.getPdf(this.snapshot.id);
      this.pdfUrl = result.downloadUrl;
      window.open(this.pdfUrl, '_blank', 'noopener');
    } finally {
      this.working = false;
    }
  }

  openYeuCauBoSung(): void {
    if (!this.selectedKyBaoCaoId) {
      return;
    }

    void this.router.navigate(['/yeu-cau-bo-sung'], {
      queryParams: { kyBaoCaoId: this.selectedKyBaoCaoId },
    });
  }

  snapshotStatusText(status?: number): string {
    switch (status) {
      case 1:
        return 'Draft';
      case 2:
        return 'Submitted';
      case 3:
        return 'Locked';
      case 4:
        return 'Superseded';
      default:
        return 'Chưa có snapshot';
    }
  }

  kyStatusText(status: number): string {
    switch (status) {
      case 1:
        return 'Chuẩn bị';
      case 2:
        return 'Đang mở';
      case 3:
        return 'Đã đóng';
      case 4:
        return 'Khóa';
      default:
        return 'Không xác định';
    }
  }

  trackSnapshot(index: number, item: SnapshotDto): number {
    return item.id || index;
  }

  private async loadSnapshotsForKy(
    kyBaoCaoId: number,
    preferredSnapshotId?: number,
  ): Promise<void> {
    const snapshots = await this.snapshotApi.getByKy(kyBaoCaoId);
    this.snapshots = snapshots.filter(
      (item) => !this.currentDonViId || item.donViId === this.currentDonViId,
    );

    const nextSnapshot = preferredSnapshotId
      ? (this.snapshots.find((item) => item.id === preferredSnapshotId) ??
        this.snapshots[0])
      : this.snapshots[0];

    if (nextSnapshot) {
      await this.selectSnapshot(nextSnapshot);
      return;
    }

    this.snapshot = null;
    this.form.patchValue({
      snapshotJson: '',
      summaryJson: '',
      ghiChu: '',
    });
  }
}
