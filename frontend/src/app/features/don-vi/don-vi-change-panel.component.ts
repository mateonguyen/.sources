import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnChanges, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DropdownModule } from 'primeng/dropdown';
import { MultiSelectModule } from 'primeng/multiselect';
import { CheckboxModule } from 'primeng/checkbox';
import { DonViDto } from './don-vi.api';
import {
  DonViChangeApi, DonViChangePreviewDto, DonViSuccessorDto, DonViVersionDto,
  MergeDonViRequest, SplitDonViRequest,
} from './don-vi-change.api';
import {
  IdentityAdminApi, RoleDto, TransferUsersPreviewDto, TransferUsersRequest, UserDto,
} from '../identity-admin/identity-admin.api';
import { ConfirmDialogWrapperService } from '../../shared/ui/confirm-dialog-wrapper.service';
import { NotificationService } from '../../core/ui/notification.service';

type ChangeMode = 'merge' | 'split' | 'users' | 'history';
interface ChildRow { id: number; name: string; selected: boolean; targetId: number | null; }
interface UserRow { id: number; name: string; selected: boolean; targetId: number | null; roleIds: number[]; }

@Component({
  selector: 'app-don-vi-change-panel',
  standalone: true,
  imports: [CommonModule, FormsModule, ButtonModule, DropdownModule, MultiSelectModule, CheckboxModule],
  templateUrl: './don-vi-change-panel.component.html',
  styleUrl: './don-vi-change-panel.component.scss',
})
export class DonViChangePanelComponent implements OnChanges {
  @Input() units: DonViDto[] = [];
  @Output() changed = new EventEmitter<void>();

  mode: ChangeMode = 'merge';
  allUnits: DonViDto[] = [];
  activeUnitOptions: Array<{ label: string; value: number }> = [];
  allUnitOptions: Array<{ label: string; value: number }> = [];
  users: UserDto[] = [];
  roles: RoleDto[] = [];
  busy = false;
  error = '';
  preview: DonViChangePreviewDto | TransferUsersPreviewDto | null = null;

  mergeSources: number[] = [];
  mergeTarget: number | null = null;
  mergeNote = '';
  splitSource: number | null = null;
  splitChildren: ChildRow[] = [];
  splitUsers: UserRow[] = [];
  splitDeactivate = false;
  splitNote = '';
  transferSource: number | null = null;
  transferTarget: number | null = null;
  transferUserIds: number[] = [];
  transferRoleIds: number[] = [];
  historyUnit: number | null = null;
  versions: DonViVersionDto[] = [];
  successors: DonViSuccessorDto[] = [];

  constructor(
    private readonly api: DonViChangeApi,
    private readonly identityApi: IdentityAdminApi,
    private readonly confirmDialog: ConfirmDialogWrapperService,
    private readonly notifications: NotificationService,
  ) {
    void this.loadIdentity();
  }

  ngOnChanges(): void {
    const flatten = (nodes: DonViDto[]): DonViDto[] => nodes.flatMap(node => [node, ...flatten(node.children ?? [])]);
    this.allUnits = flatten(this.units);
    this.activeUnitOptions = this.allUnits.filter(x => x.isActive)
      .map(x => ({ label: `${x.maDonVi} - ${x.tenDonVi}`, value: x.id }));
    this.allUnitOptions = this.allUnits.map(x => ({
      label: `${x.maDonVi} - ${x.tenDonVi}${x.isActive ? '' : ' (ngừng)'}`, value: x.id,
    }));
    if (this.splitSource) this.prepareSplit();
  }

  get roleOptions(): Array<{ label: string; value: number }> {
    return this.roles.map(x => ({ label: x.tenRole, value: x.id }));
  }
  get transferUserOptions(): Array<{ label: string; value: number }> {
    return this.users.filter(x => x.isActive && x.donViId === this.transferSource)
      .map(x => ({ label: `${x.hoTen} (${x.username})`, value: x.id }));
  }
  get previewErrors(): string[] { return this.preview?.errors ?? []; }
  get canExecute(): boolean { return !!this.preview && this.previewErrors.length === 0 && !this.busy; }

  setMode(mode: ChangeMode): void { this.mode = mode; this.invalidate(); }
  invalidate(): void { this.preview = null; this.error = ''; }

  prepareSplit(): void {
    this.invalidate();
    const previousChildren = new Map(this.splitChildren.map(x => [x.id, x]));
    const previousUsers = new Map(this.splitUsers.map(x => [x.id, x]));
    this.splitChildren = this.allUnits.filter(x => x.isActive && x.parentId === this.splitSource)
      .map(x => previousChildren.get(x.id) ?? { id: x.id, name: x.tenDonVi, selected: false, targetId: null });
    this.splitUsers = this.users.filter(x => x.isActive && x.donViId === this.splitSource)
      .map(x => previousUsers.get(x.id) ?? { id: x.id, name: `${x.hoTen} (${x.username})`, selected: false, targetId: null, roleIds: [] });
  }

  private async loadIdentity(): Promise<void> {
    try {
      [this.users, this.roles] = await Promise.all([this.identityApi.getUsers(), this.identityApi.getRoles()]);
      if (this.splitSource) this.prepareSplit();
    } catch { this.error = 'Không tải được tài khoản hoặc vai trò.'; }
  }

  private mergePayload(): MergeDonViRequest {
    return { sourceIds: this.mergeSources, targetId: this.mergeTarget ?? 0, note: this.mergeNote.trim() || undefined };
  }
  private splitPayload(): SplitDonViRequest {
    return {
      sourceId: this.splitSource ?? 0,
      children: this.splitChildren.filter(x => x.selected).map(x => ({ childId: x.id, targetId: x.targetId ?? 0 })),
      users: this.splitUsers.filter(x => x.selected).map(x => ({ userId: x.id, targetId: x.targetId ?? 0, roleIds: x.roleIds })),
      deactivateSource: this.splitDeactivate,
      note: this.splitNote.trim() || undefined,
    };
  }
  private transferPayload(): TransferUsersRequest {
    return { items: this.transferUserIds.map(userId => ({ userId, sourceDonViId: this.transferSource ?? 0, targetDonViId: this.transferTarget ?? 0, roleIds: this.transferRoleIds })) };
  }

  async runPreview(): Promise<void> {
    this.busy = true;
    this.invalidate();
    try {
      if (this.mode === 'merge') this.preview = await this.api.previewMerge(this.mergePayload());
      if (this.mode === 'split') this.preview = await this.api.previewSplit(this.splitPayload());
      if (this.mode === 'users') this.preview = await this.identityApi.previewTransfers(this.transferPayload());
    } catch (error) { this.error = this.errorMessage(error); }
    finally { this.busy = false; }
  }

  async execute(): Promise<void> {
    if (!this.canExecute) return;
    const count = this.mode === 'users' ? (this.preview as TransferUsersPreviewDto).userCount : (this.preview as DonViChangePreviewDto).unitCount;
    const confirmed = await this.confirmDialog.confirm({
      header: 'Xác nhận biến động',
      message: `Thực hiện tác vụ đã xem trước (${count} ${this.mode === 'users' ? 'tài khoản' : 'đơn vị'})? Lịch sử báo cáo sẽ được giữ nguyên.`,
      acceptLabel: 'Thực hiện',
      countdownSeconds: 5,
    });
    if (!confirmed) return;
    this.busy = true;
    try {
      if (this.mode === 'merge') await this.api.merge(this.mergePayload());
      if (this.mode === 'split') await this.api.split(this.splitPayload());
      if (this.mode === 'users') await this.identityApi.transferUsers(this.transferPayload());
      this.notifications.show('success', 'Tác vụ hoàn tất. Hãy kiểm tra cây đơn vị và tài khoản.');
      this.invalidate();
      await this.loadIdentity();
      this.changed.emit();
    } catch (error) { this.error = this.errorMessage(error); }
    finally { this.busy = false; }
  }

  async loadHistory(): Promise<void> {
    if (!this.historyUnit) return;
    this.busy = true;
    this.error = '';
    try {
      [this.versions, this.successors] = await Promise.all([
        this.api.versions(this.historyUnit), this.api.successors(this.historyUnit),
      ]);
    } catch (error) { this.error = this.errorMessage(error); }
    finally { this.busy = false; }
  }

  unitLabel(id: number): string { return this.allUnits.find(x => x.id === id)?.tenDonVi ?? `${id}`; }
  private errorMessage(error: unknown): string {
    return (error as { error?: { error?: { message?: string } } })?.error?.error?.message ?? 'Không thực hiện được tác vụ.';
  }
}
