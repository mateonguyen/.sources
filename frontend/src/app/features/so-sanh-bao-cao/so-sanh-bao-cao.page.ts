import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { DropdownModule } from 'primeng/dropdown';
import { TableModule } from 'primeng/table';
import { NotificationService } from '../../core/ui/notification.service';
import { EmptyStateComponent } from '../../shared/ui/empty-state.component';
import { LoadingOverlayComponent } from '../../shared/ui/loading-overlay.component';
import { PageHeaderComponent } from '../../shared/ui/page-header.component';
import { BienDongBaoCaoApi, ChangeMetric, ChangeModule, ChangeOptions, ChangeOverview, ChangePeriodOption, ChangeStatus, ChangeUnit, CurrentMetric, CurrentModule, CurrentOverview, CurrentUnit, ModuleRecordSection, ModuleRecords } from './bien-dong-bao-cao.api';

type AnalysisMode = 'CURRENT' | 'CHANGE';

@Component({
  selector: 'app-so-sanh-bao-cao-page',
  standalone: true,
  imports: [CommonModule, FormsModule, PageHeaderComponent, EmptyStateComponent, LoadingOverlayComponent, ButtonModule, DialogModule, DropdownModule, TableModule],
  templateUrl: './so-sanh-bao-cao.page.html',
  styleUrls: ['./so-sanh-bao-cao.page.scss'],
})
export class SoSanhBaoCaoPage implements OnInit {
  loading = false;
  options: ChangeOptions | null = null;
  overview: ChangeOverview | null = null;
  currentOverview: CurrentOverview | null = null;
  analysisMode: AnalysisMode = 'CURRENT';
  selectedToPeriodId: number | null = null;
  selectedFromPeriodId: number | null = null;
  selectedUnit: ChangeUnit | null = null;
  selectedCurrentUnit: CurrentUnit | null = null;
  unitFilterText = '';
  moduleFilterText = '';
  expandedModuleCode: string | null = null;
  moduleSearchText = '';
  recordsDialogVisible = false;
  recordsDialogLoading = false;
  recordsDialogTitle = '';
  recordsDialogData: ModuleRecords | null = null;
  recordsSearchText = '';

  readonly statusLabel: Record<ChangeStatus, string> = {
    IMPROVING: 'Cải thiện', WORSENING: 'Cần chú ý', STABLE: 'Ổn định', CHANGED: 'Có biến động', MISSING: 'Thiếu dữ liệu',
  };

  constructor(private readonly api: BienDongBaoCaoApi, private readonly notification: NotificationService) {}

  ngOnInit(): void { void this.loadOptions(); }

  get toPeriodOptions(): ChangePeriodOption[] {
    return this.sortPeriods(this.options?.kyBaoCaos ?? []);
  }

  get currentUnitLabel(): string {
    const currentUnit = (this.options?.donVis ?? []).find((unit) => unit.id === this.options?.currentDonViId);
    return currentUnit ? `${currentUnit.tenDonVi} (${currentUnit.maDonVi})` : 'Đơn vị của tôi';
  }

  get compatibleFromPeriods(): ChangePeriodOption[] {
    const target = this.getPeriod(this.selectedToPeriodId);
    if (!target) return [];
    return this.sortPeriods(
      (this.options?.kyBaoCaos ?? []).filter(
        (x) => x.id !== target.id && x.mauBaoCaoId === target.mauBaoCaoId,
      ),
    );
  }

  get fromPeriodOptions(): ChangePeriodOption[] { return this.compatibleFromPeriods; }

  get filteredCurrentUnits(): CurrentUnit[] {
    return this.filterUnitsByText(this.currentOverview?.units ?? []);
  }

  get filteredChangeUnits(): ChangeUnit[] {
    return this.filterUnitsByText(this.overview?.units ?? []);
  }

  get filteredCurrentUnitModules(): CurrentModule[] {
    return this.filterModulesByText(this.selectedCurrentUnit?.modules ?? []);
  }

  get filteredChangeUnitModules(): ChangeModule[] {
    return this.filterModulesByText(this.selectedUnit?.modules ?? []);
  }

  get expandedCurrentModule(): CurrentModule | null {
    return (this.selectedCurrentUnit?.modules ?? []).find((x) => x.moduleCode === this.expandedModuleCode) ?? null;
  }

  get expandedChangeModule(): ChangeModule | null {
    return (this.selectedUnit?.modules ?? []).find((x) => x.moduleCode === this.expandedModuleCode) ?? null;
  }

  get filteredExpandedCurrentMetrics(): CurrentMetric[] {
    return this.filterMetricsByName(this.expandedCurrentModule?.metrics ?? []);
  }

  get filteredExpandedChangeMetrics(): ChangeMetric[] {
    return this.filterMetricsByName(this.expandedChangeModule?.metrics ?? []);
  }

  get filteredRecordSections(): ModuleRecordSection[] {
    const text = this.recordsSearchText.trim().toLowerCase();
    const sections = this.recordsDialogData?.sections ?? [];
    if (!text) return sections;
    return sections
      .map((section) => ({ ...section, rows: section.rows.filter((row) => row.some((cell) => cell != null && String(cell).toLowerCase().includes(text))) }))
      .filter((section) => section.rows.length > 0);
  }

  get recordsTotalRows(): number {
    return (this.recordsDialogData?.sections ?? []).reduce((sum, section) => sum + section.rows.length, 0);
  }

  async loadOptions(): Promise<void> {
    this.loading = true;
    try {
      this.options = await this.api.getOptions();
      const sortedPeriods = this.sortPeriods(this.options.kyBaoCaos);
      const preferredTarget = sortedPeriods.find((candidate) =>
        this.options?.kyBaoCaos.some(
          (other) =>
            other.id !== candidate.id &&
            other.mauBaoCaoId === candidate.mauBaoCaoId,
        ),
      );
      this.selectedToPeriodId =
        preferredTarget?.id ?? sortedPeriods[0]?.id ?? null;
      this.applyCompatibleBaseline();
      if (this.selectedToPeriodId) await this.loadAnalysis();
    } catch {
      this.notification.show('error', 'Không thể tải dữ liệu biến động và xu hướng.');
    } finally { this.loading = false; }
  }

  onTargetPeriodChange(): void {
    this.applyCompatibleBaseline();
    this.overview = null;
    this.currentOverview = null;
    this.selectedUnit = null;
    this.selectedCurrentUnit = null;
    this.unitFilterText = '';
    this.moduleFilterText = '';
    this.closeModuleDetail();
    void this.loadAnalysis();
  }

  onFromPeriodChange(): void {
    this.overview = null;
    this.selectedUnit = null;
    this.unitFilterText = '';
    this.moduleFilterText = '';
    this.closeModuleDetail();
    void this.loadOverview();
  }

  setAnalysisMode(mode: AnalysisMode): void {
    if (this.analysisMode === mode) return;
    this.analysisMode = mode;
    this.overview = null;
    this.currentOverview = null;
    this.selectedUnit = null;
    this.selectedCurrentUnit = null;
    this.unitFilterText = '';
    this.moduleFilterText = '';
    this.closeModuleDetail();
    void this.loadAnalysis();
  }

  async loadAnalysis(): Promise<void> {
    if (this.analysisMode === 'CURRENT') await this.loadCurrent();
    else await this.loadOverview();
  }

  async loadCurrent(): Promise<void> {
    if (!this.selectedToPeriodId) {
      this.notification.show('warning', 'Vui lòng chọn kỳ báo cáo để xem hiện trạng.');
      return;
    }
    this.loading = true;
    try {
      this.currentOverview = await this.api.getCurrent(this.selectedToPeriodId);
      this.selectedCurrentUnit = this.currentOverview.units.find((x) => x.donViId === this.selectedCurrentUnit?.donViId)
        ?? this.currentOverview.units[0]
        ?? null;
    } catch {
      this.currentOverview = null;
      this.selectedCurrentUnit = null;
      this.notification.show('error', 'Không thể tải hiện trạng dữ liệu cho điều kiện đã chọn.');
    } finally { this.loading = false; }
  }

  async loadOverview(): Promise<void> {
    if (!this.selectedToPeriodId || !this.selectedFromPeriodId) {
      this.notification.show('warning', 'Cần có hai kỳ cùng mẫu báo cáo để phân tích biến động.');
      return;
    }
    this.loading = true;
    try {
      this.overview = await this.api.getOverview(this.selectedToPeriodId, this.selectedFromPeriodId);
      this.selectedUnit = this.overview.units.find((x) => x.donViId === this.selectedUnit?.donViId) ?? this.overview.units[0] ?? null;
    } catch {
      this.overview = null;
      this.selectedUnit = null;
      this.notification.show('error', 'Không thể tổng hợp biến động cho điều kiện đã chọn.');
    } finally { this.loading = false; }
  }

  selectUnit(unit: ChangeUnit): void { this.selectedUnit = unit; this.moduleFilterText = ''; this.closeModuleDetail(); }
  selectCurrentUnit(unit: CurrentUnit): void { this.selectedCurrentUnit = unit; this.moduleFilterText = ''; this.closeModuleDetail(); }
  toggleModule(moduleCode: string): void {
    this.expandedModuleCode = this.expandedModuleCode === moduleCode ? null : moduleCode;
    this.moduleSearchText = '';
  }
  closeModuleDetail(): void { this.expandedModuleCode = null; this.moduleSearchText = ''; }

  async openRecordsDialog(moduleCode: string, moduleName: string, kyBaoCaoId: number | undefined, donViId: number | undefined): Promise<void> {
    if (!kyBaoCaoId || !donViId) return;
    this.recordsDialogVisible = true;
    this.recordsDialogLoading = true;
    this.recordsDialogTitle = moduleName;
    this.recordsSearchText = '';
    this.recordsDialogData = null;
    try {
      this.recordsDialogData = await this.api.getModuleRecords(kyBaoCaoId, donViId, moduleCode);
    } catch {
      this.notification.show('error', 'Không thể tải danh sách chi tiết của module này.');
      this.recordsDialogVisible = false;
    } finally {
      this.recordsDialogLoading = false;
    }
  }

  closeRecordsDialog(): void { this.recordsDialogVisible = false; this.recordsDialogData = null; this.recordsSearchText = ''; }
  formatCell(cell: string | number | boolean | null): string {
    if (cell == null || cell === '') return '—';
    if (typeof cell === 'boolean') return cell ? 'Có' : 'Không';
    return String(cell);
  }
  private filterMetricsByName<T extends { metricName: string }>(metrics: T[]): T[] {
    const text = this.moduleSearchText.trim().toLowerCase();
    return text ? metrics.filter((x) => x.metricName.toLowerCase().includes(text)) : metrics;
  }
  private filterUnitsByText<T extends { tenDonVi: string; maDonVi: string }>(units: T[]): T[] {
    const text = this.unitFilterText.trim().toLowerCase();
    return text ? units.filter((x) => x.tenDonVi.toLowerCase().includes(text) || x.maDonVi.toLowerCase().includes(text)) : units;
  }
  private filterModulesByText<T extends { moduleName: string }>(modules: T[]): T[] {
    const text = this.moduleFilterText.trim().toLowerCase();
    return text ? modules.filter((x) => x.moduleName.toLowerCase().includes(text)) : modules;
  }
  primaryMetric(module: ChangeModule): ChangeMetric | null { return module.metrics.find((x) => x.isPrimary) ?? module.metrics[0] ?? null; }
  primaryCurrentMetric(module: CurrentModule): CurrentMetric | null { return module.metrics.find((x) => x.isPrimary) ?? module.metrics[0] ?? null; }
  formatValue(value: number | undefined, unit: string): string {
    if (value == null) return '—';
    return `${new Intl.NumberFormat('vi-VN', { maximumFractionDigits: unit === '%' ? 2 : 0 }).format(value)}${unit === '%' ? '%' : ''}`;
  }
  formatDelta(value: number | undefined): string {
    if (value == null) return '—';
    return `${value > 0 ? '+' : ''}${new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 2 }).format(value)}`;
  }
  getStatusLabel(status: ChangeStatus): string { return this.statusLabel[status] ?? 'Chưa xác định'; }
  trackByUnit(_: number, unit: ChangeUnit): number { return unit.donViId; }
  trackByCurrentUnit(_: number, unit: CurrentUnit): number { return unit.donViId; }
  private applyCompatibleBaseline(): void { this.selectedFromPeriodId = this.compatibleFromPeriods[0]?.id ?? null; }
  private getPeriod(id: number | null): ChangePeriodOption | null { return (this.options?.kyBaoCaos ?? []).find((x) => x.id === id) ?? null; }

  private sortPeriods(periods: ChangePeriodOption[]): ChangePeriodOption[] {
    return [...periods].sort((a, b) => {
      if (a.nam !== b.nam) return b.nam - a.nam;

      const aPosition = a.thang ?? (a.quy ? a.quy * 3 : 12);
      const bPosition = b.thang ?? (b.quy ? b.quy * 3 : 12);
      if (aPosition !== bPosition) return bPosition - aPosition;

      return b.id - a.id;
    });
  }
}
