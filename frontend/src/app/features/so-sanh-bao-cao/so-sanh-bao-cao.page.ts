import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DropdownModule } from 'primeng/dropdown';
import { TableModule } from 'primeng/table';
import { NotificationService } from '../../core/ui/notification.service';
import { EmptyStateComponent } from '../../shared/ui/empty-state.component';
import { LoadingOverlayComponent } from '../../shared/ui/loading-overlay.component';
import { SectionCardComponent } from '../../shared/ui/section-card.component';
import {
  SnapshotApi,
  SnapshotCompareDto,
  SnapshotCompareKyOptionDto,
  SnapshotCompareOptionDto,
} from '../snapshot/snapshot.api';

@Component({
  selector: 'app-so-sanh-bao-cao-page',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    SectionCardComponent,
    EmptyStateComponent,
    LoadingOverlayComponent,
    ButtonModule,
    DropdownModule,
    TableModule,
  ],
  templateUrl: './so-sanh-bao-cao.page.html',
  styleUrls: ['./so-sanh-bao-cao.page.scss'],
})
export class SoSanhBaoCaoPage implements OnInit {
  loading = false;
  comparing = false;
  onlyChanged = false;
  sortMode: 'moduleAsc' | 'deltaDesc' | 'deltaAsc' | 'absDeltaDesc' =
    'absDeltaDesc';
  options: SnapshotCompareOptionDto[] = [];
  selectedDonViId: number | null = null;
  fromKyBaoCaoId: number | null = null;
  toKyBaoCaoId: number | null = null;
  compareResult: SnapshotCompareDto | null = null;

  readonly sortOptions = [
    { label: 'Ưu tiên chênh lệch lớn nhất', value: 'absDeltaDesc' as const },
    { label: 'Theo module (A-Z)', value: 'moduleAsc' as const },
    { label: 'Chênh lệch tăng dần', value: 'deltaAsc' as const },
    { label: 'Chênh lệch giảm dần', value: 'deltaDesc' as const },
  ];

  constructor(
    private readonly snapshotApi: SnapshotApi,
    private readonly notification: NotificationService,
  ) {}

  ngOnInit(): void {
    void this.loadOptions();
  }

  get donViOptions(): { label: string; value: number }[] {
    return this.options.map((x) => ({
      label: `${x.tenDonVi} (${x.kyOptions.length} kỳ)`,
      value: x.donViId,
    }));
  }

  get kyOptions(): { label: string; value: number }[] {
    return (
      this.selectedDonVi?.kyOptions.map((x) => ({
        label: x.kyCode,
        value: x.kyBaoCaoId,
      })) ?? []
    );
  }

  get selectedDonVi(): SnapshotCompareOptionDto | null {
    if (!this.selectedDonViId) {
      return null;
    }
    return this.options.find((x) => x.donViId === this.selectedDonViId) ?? null;
  }

  get selectedKyInfo(): SnapshotCompareKyOptionDto[] {
    return this.selectedDonVi?.kyOptions ?? [];
  }

  get displayedModules() {
    const modules = this.compareResult?.modules ?? [];
    const filtered = this.onlyChanged
      ? modules.filter((x) => x.delta !== 0)
      : modules;

    const sorted = [...filtered];
    switch (this.sortMode) {
      case 'moduleAsc':
        sorted.sort((a, b) => a.moduleCode.localeCompare(b.moduleCode, 'vi'));
        break;
      case 'deltaAsc':
        sorted.sort((a, b) => a.delta - b.delta);
        break;
      case 'deltaDesc':
        sorted.sort((a, b) => b.delta - a.delta);
        break;
      default:
        sorted.sort((a, b) => Math.abs(b.delta) - Math.abs(a.delta));
        break;
    }

    return sorted;
  }

  get changedModuleCount(): number {
    return (this.compareResult?.modules ?? []).filter((x) => x.delta !== 0)
      .length;
  }

  get upModuleCount(): number {
    return (this.compareResult?.modules ?? []).filter((x) => x.delta > 0)
      .length;
  }

  get downModuleCount(): number {
    return (this.compareResult?.modules ?? []).filter((x) => x.delta < 0)
      .length;
  }

  async loadOptions(): Promise<void> {
    this.loading = true;
    try {
      this.options = await this.snapshotApi.getCompareOptions();
      if (this.options.length === 0) {
        this.selectedDonViId = null;
        return;
      }

      this.selectedDonViId = this.options[0].donViId;
      this.applyDefaultKySelection();
    } catch {
      this.notification.show(
        'error',
        'Không thể tải dữ liệu cho chức năng so sánh báo cáo.',
      );
    } finally {
      this.loading = false;
    }
  }

  onDonViChange(): void {
    this.compareResult = null;
    this.applyDefaultKySelection();
  }

  applyDefaultKySelection(): void {
    const ky = this.selectedKyInfo;
    this.fromKyBaoCaoId = ky[1]?.kyBaoCaoId ?? ky[0]?.kyBaoCaoId ?? null;
    this.toKyBaoCaoId = ky[0]?.kyBaoCaoId ?? null;
  }

  swapKy(): void {
    const from = this.fromKyBaoCaoId;
    this.fromKyBaoCaoId = this.toKyBaoCaoId;
    this.toKyBaoCaoId = from;
    this.compareResult = null;
  }

  async compare(): Promise<void> {
    if (!this.selectedDonViId || !this.fromKyBaoCaoId || !this.toKyBaoCaoId) {
      this.notification.show(
        'warning',
        'Vui lòng chọn đơn vị và 2 kỳ báo cáo trước khi so sánh.',
      );
      return;
    }

    if (this.fromKyBaoCaoId === this.toKyBaoCaoId) {
      this.notification.show(
        'warning',
        'Hai kỳ báo cáo so sánh phải khác nhau.',
      );
      return;
    }

    this.comparing = true;
    try {
      this.compareResult = await this.snapshotApi.compareTwoKy(
        this.selectedDonViId,
        this.fromKyBaoCaoId,
        this.toKyBaoCaoId,
      );
    } catch {
      this.compareResult = null;
      this.notification.show(
        'error',
        'Không thể so sánh 2 kỳ báo cáo của đơn vị đã chọn.',
      );
    } finally {
      this.comparing = false;
    }
  }
}
