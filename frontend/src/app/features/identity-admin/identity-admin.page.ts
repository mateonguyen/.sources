import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DropdownModule } from 'primeng/dropdown';
import { InputTextModule } from 'primeng/inputtext';
import { PaginatorState, PaginatorModule } from 'primeng/paginator';
import { CheckboxModule } from 'primeng/checkbox';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { NotificationService } from '../../core/ui/notification.service';
import { ConfirmDialogWrapperService } from '../../shared/ui/confirm-dialog-wrapper.service';
import { CheckboxIndeterminateDirective } from '../../shared/directives/checkbox-indeterminate.directive';
import { EmptyStateComponent } from '../../shared/ui/empty-state.component';
import { LoadingOverlayComponent } from '../../shared/ui/loading-overlay.component';
import { SectionCardComponent } from '../../shared/ui/section-card.component';
import {
  AssignRolesRequest,
  CreateRoleRequest,
  CreateUserRequest,
  IdentityAdminApi,
  PermissionItemDto,
  RoleDto,
  RolePermissionMappingDto,
  UpdateRoleRequest,
  UpdateRolePermissionsRequest,
  UserDto,
  UserRoleMappingDto,
  DonViDto,
} from './identity-admin.api';
import {
  UserDialogMode,
  UserUpsertDialogComponent,
  UserUpsertInitialData,
  UserUpsertSubmitPayload,
} from './user-upsert-dialog.component';
import {
  RoleDialogMode,
  RoleUpsertDialogComponent,
  RoleUpsertInitialData,
  RoleUpsertSubmitPayload,
} from './role-upsert-dialog.component';

type IdentityModule = 'users' | 'roles' | 'permissions' | 'phan-quyen';
type PermissionAdminTab = 'role' | 'user';
type BusinessGroupKey =
  | 'system_admin'
  | 'reports'
  | 'hr_it'
  | 'infrastructure'
  | 'documents'
  | 'security'
  | 'other';
type PermissionActionKey =
  | 'read'
  | 'create'
  | 'update'
  | 'delete'
  | 'approve'
  | 'submit'
  | 'other';

interface PermissionActionColumn {
  key: PermissionActionKey;
  label: string;
}

interface BusinessGroupDefinition {
  key: BusinessGroupKey;
  label: string;
  keywords: string[];
}

interface ParsedPermissionEntry {
  permissionId: number;
  permCode: string;
  businessGroupKey: BusinessGroupKey;
  businessGroupLabel: string;
  resourceKey: string;
  resourceLabel: string;
  action: PermissionActionKey;
}

interface PermissionMatrixRow {
  resourceKey: string;
  resourceLabel: string;
  businessGroupKey: BusinessGroupKey;
  businessGroupLabel: string;
  permissionIdByAction: Partial<Record<PermissionActionKey, number>>;
  permCodeByAction: Partial<Record<PermissionActionKey, string>>;
  allowedActions: PermissionActionKey[];
  selectedActions: PermissionActionKey[];
  permissionIds: number[];
}

interface PermissionMatrixModuleGroup {
  businessGroupKey: BusinessGroupKey;
  businessGroupLabel: string;
  rows: PermissionMatrixRow[];
  permissionIds: number[];
  actionPermissionIds: Partial<Record<PermissionActionKey, number[]>>;
}

@Component({
  selector: 'app-identity-admin-page',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    SectionCardComponent,
    LoadingOverlayComponent,
    EmptyStateComponent,
    TableModule,
    TagModule,
    ButtonModule,
    ConfirmDialogModule,
    DropdownModule,
    InputTextModule,
    PaginatorModule,
    CheckboxModule,
    TooltipModule,
    CheckboxIndeterminateDirective,
    UserUpsertDialogComponent,
    RoleUpsertDialogComponent,
  ],
  templateUrl: './identity-admin.page.html',
  styleUrl: './identity-admin.page.scss',
})
export class IdentityAdminPage {
  private static readonly LOAD_TIMEOUT_MS = 12000;
  private static readonly SYSTEM_ADMIN_PERMISSION_CODE = 'system:admin';

  module: IdentityModule = 'users';
  loading = false;
  apiError = '';
  tableLoading = false;

  users: UserDto[] = [];
  roles: RoleDto[] = [];
  permissions: PermissionItemDto[] = [];
  userRoleMappings: UserRoleMappingDto[] = [];
  rolePermissionMappings: RolePermissionMappingDto[] = [];
  donVis: DonViDto[] = [];

  readonly pageSizeOptions = [10, 20, 50];
  readonly pageSizeDropdownOptions = [10, 20, 50].map((n) => ({
    label: `${n}`,
    value: n,
  }));
  rows = 10;
  first = 0;

  searchTerm = '';
  selectedDonViId: number | null = null;
  selectedStatus: '' | 'active' | 'inactive' = '';
  roleSearchTerm = '';
  selectedRoleType: '' | 'system' | 'business' = '';
  selectedRoleStatus: '' | 'active' | 'inactive' = 'active';
  roleFirst = 0;
  roleRows = 10;
  permissionSearchInput = '';
  permissionModuleInput = '';
  permissionActionInput = '';
  appliedPermissionSearch = '';
  appliedPermissionModule = '';
  appliedPermissionAction = '';
  permissionFirst = 0;
  permissionRows = 20;
  userDialogVisible = false;
  userDialogMode: UserDialogMode = 'create';
  userDialogSubmitting = false;
  userDialogInitialData: UserUpsertInitialData | null = null;
  roleDialogVisible = false;
  roleDialogMode: RoleDialogMode = 'create';
  roleDialogSubmitting = false;
  roleDialogInitialData: RoleUpsertInitialData | null = null;
  permissionAdminTab: PermissionAdminTab = 'role';
  selectedPermissionRoleId: number | null = null;
  rolePermissionSearchTerm = '';
  activePermissionModuleKey = '';
  selectedPermissionIds = new Set<number>();
  originalPermissionIds = new Set<number>();
  savingRolePermissions = false;
  selectedPermissionUserId: number | null = null;
  permissionMatrixModulesState: PermissionMatrixModuleGroup[] = [];
  filteredPermissionMatrixModulesState: PermissionMatrixModuleGroup[] = [];
  currentPermissionModuleState: PermissionMatrixModuleGroup | null = null;
  selectedPermissionUserPermissionsState: PermissionItemDto[] = [];
  selectedPermissionUserPermissionGroupsState: Array<{
    moduleLabel: string;
    permissions: Array<{ action: string; feature: string }>;
  }> = [];

  readonly permissionActionColumns: PermissionActionColumn[] = [
    { key: 'read', label: 'Xem' },
    { key: 'create', label: 'Thêm' },
    { key: 'update', label: 'Sửa' },
    { key: 'delete', label: 'Xóa' },
    { key: 'approve', label: 'Phê duyệt' },
    { key: 'submit', label: 'Gửi' },
    { key: 'other', label: 'Khác' },
  ];

  readonly donViOptions = [
    { label: 'Tất cả đơn vị', value: null as number | null },
  ];
  readonly statusOptions = [
    { label: 'Tất cả trạng thái', value: '' as '' | 'active' | 'inactive' },
    { label: 'Đang hoạt động', value: 'active' as '' | 'active' | 'inactive' },
    {
      label: 'Ngừng hoạt động',
      value: 'inactive' as '' | 'active' | 'inactive',
    },
  ];
  readonly roleTypeOptions = [
    {
      label: 'Tất cả loại vai trò',
      value: '' as '' | 'system' | 'business',
    },
    {
      label: 'Hệ thống',
      value: 'system' as '' | 'system' | 'business',
    },
    {
      label: 'Nghiệp vụ',
      value: 'business' as '' | 'system' | 'business',
    },
  ];
  readonly roleStatusOptions = [
    { label: 'Tất cả trạng thái', value: '' as '' | 'active' | 'inactive' },
    { label: 'Đang hoạt động', value: 'active' as '' | 'active' | 'inactive' },
    {
      label: 'Ngừng hoạt động',
      value: 'inactive' as '' | 'active' | 'inactive',
    },
  ];

  // Essential: Module code to label map (small, ~30 items)
  private readonly moduleCodeLabelMap: Record<string, string> = {
    users: 'Người dùng',
    roles: 'Vai trò',
    permissions: 'Phân quyền',
    phan_quyen: 'Phân quyền',
    codes: 'Danh mục',
    don_vi: 'Đơn vị',
    ky_bao_cao: 'Kỳ báo cáo',
    mau_bao_cao: 'Mẫu báo cáo',
    bao_cao: 'Báo cáo',
    snapshot: 'Báo cáo snapshot',
    nhan_luc_cntt: 'Nhân lực CNTT',
    nang_luc_so: 'Năng lực số',
    dao_tao_boi_duong: 'Đào tạo bồi dưỡng',
    dao_tao_hoc_vien: 'Đào tạo học viện',
    he_thong_thong_tin: 'Hệ thống thông tin',
    du_an_cntt: 'Dự án CNTT',
    ha_tang_mang: 'Hạ tầng mạng',
    thiet_bi_cntt: 'Thiết bị CNTT',
    camera_thuc_trang: 'Camera thực trạng',
    camera_quan_ly: 'Camera quản lý',
    van_ban_qppl: 'Văn bản quy phạm pháp luật',
    van_ban_den: 'Văn bản đến',
    van_ban_di: 'Văn bản đi',
    giam_sat_soc: 'Giám sát SOC',
    giam_sat_noc: 'Giám sát NOC',
    attt_httt_dau_tu: 'An toàn thông tin đầu tư',
    attt_httt_van_hanh: 'An toàn thông tin vận hành',
    giai_phap_attt: 'Giải pháp ATTT',
    tong_hop_tien_do: 'Tiến độ tổng hợp',
    tien_do_bao_cao: 'Tiến độ báo cáo',
    yeu_cau_bo_sung: 'Yêu cầu bổ sung',
    thong_bao: 'Thông báo',
    files: 'Tệp tin',
    auth: 'Xác thực',
  };

  // Business group definitions for permission grouping (UI logic only)
  private readonly businessGroupDefinitions: Record<
    BusinessGroupKey,
    BusinessGroupDefinition
  > = {
    system_admin: {
      key: 'system_admin',
      label: 'Quản trị hệ thống',
      keywords: [
        'quan tri',
        'system',
        'admin',
        'nguoi dung',
        'vai tro',
        'users',
        'roles',
        'permissions',
        'codes',
        'don_vi',
      ],
    },
    reports: {
      key: 'reports',
      label: 'Báo cáo & tổng hợp',
      keywords: [
        'bao cao',
        'tong hop',
        'ky bao cao',
        'mau bao cao',
        'snapshot',
        'reports',
        'tien do tong hop',
        'yeu cau bo sung',
      ],
    },
    hr_it: {
      key: 'hr_it',
      label: 'Nhân lực CNTT',
      keywords: ['nhan luc', 'cntt', 'dao tao', 'nang luc so'],
    },
    infrastructure: {
      key: 'infrastructure',
      label: 'Hạ tầng & hệ thống',
      keywords: ['ha tang', 'he thong', 'camera', 'thiet bi', 'mang', 'du an'],
    },
    documents: {
      key: 'documents',
      label: 'Văn bản & quản lý',
      keywords: ['van ban', 'qppl', 'den', 'di'],
    },
    security: {
      key: 'security',
      label: 'An toàn thông tin',
      keywords: ['attt', 'an toan', 'soc', 'giam sat'],
    },
    other: {
      key: 'other',
      label: 'Khác',
      keywords: ['khac'],
    },
  };

  // Maps resource code to business group for categorization
  private readonly resourceToBusinessGroupMap: Record<
    string,
    BusinessGroupKey
  > = {
    users: 'system_admin',
    roles: 'system_admin',
    permissions: 'system_admin',
    phan_quyen: 'system_admin',
    codes: 'system_admin',
    danh_muc: 'system_admin',
    don_vi: 'system_admin',
    ky_bao_cao: 'reports',
    mau_bao_cao: 'reports',
    reports: 'reports',
    snapshot: 'reports',
    tong_hop_tien_do: 'reports',
    tien_do_bao_cao: 'reports',
    yeu_cau_bo_sung: 'reports',
    nhan_luc_cntt: 'hr_it',
    nang_luc_so: 'hr_it',
    dao_tao_boi_duong: 'hr_it',
    dao_tao_hoc_vien: 'hr_it',
    he_thong_thong_tin: 'infrastructure',
    du_an_cntt: 'infrastructure',
    ha_tang_mang: 'infrastructure',
    thiet_bi_cntt: 'infrastructure',
    camera_thuc_trang: 'infrastructure',
    camera_quan_ly: 'infrastructure',
    giam_sat_noc: 'infrastructure',
    van_ban_qppl: 'documents',
    van_ban_den: 'documents',
    van_ban_di: 'documents',
    giam_sat_soc: 'security',
    attt_httt_dau_tu: 'security',
    attt_httt_van_hanh: 'security',
    giai_phap_attt: 'security',
    thong_bao: 'other',
    files: 'other',
    auth: 'other',
  };

  constructor(
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly identityApi: IdentityAdminApi,
    private readonly notificationService: NotificationService,
    private readonly confirmDialog: ConfirmDialogWrapperService,
  ) {
    this.module =
      (this.route.snapshot.data['identityModule'] as IdentityModule) ?? 'users';
    this.load();
  }

  get title(): string {
    switch (this.module) {
      case 'users':
        return 'Quản lý Người dùng';
      case 'roles':
        return 'Quản lý Vai trò';
      case 'permissions':
        return 'Quản lý quyền';
      default:
        return 'Phân quyền';
    }
  }

  get subtitle(): string {
    switch (this.module) {
      case 'users':
        return 'Quản lý tài khoản sử dụng hệ thống';
      case 'roles':
        return 'Quản lý danh sách vai trò và thông tin sử dụng trong hệ thống';
      case 'permissions':
        return 'Tra cứu danh mục quyền truy cập theo mô-đun và hành động';
      default:
        return 'Quan hệ gán vai trò cho người dùng và gán quyền cho vai trò.';
    }
  }

  // Truoc la getter -> tinh lai filter/map/sort tren toan bo danh sach don vi
  // (5000+ dong sau khi doi nguon du lieu) o MOI vong change-detection, gay
  // giat/delay khi mo dropdown "Don vi" trong dialog cap nhat nguoi dung.
  // Gio tinh 1 lan sau khi load xong (xem buildUserDonViOptions()).
  userDonViOptions: Array<{ label: string; value: number }> = [];
  userRoleOptions: Array<{ label: string; value: number }> = [];

  private buildUserDonViOptions(): void {
    const byId = new Map(this.donVis.map((dv) => [dv.id, dv]));
    this.userDonViOptions = this.donVis
      .filter((dv) => dv.isActive)
      .map((dv) => ({ label: this.buildDonViLabel(dv, byId), value: dv.id }))
      .sort((a, b) => a.label.localeCompare(b.label));
  }

  /** "Ten don vi — Ten don vi cha (Ma don vi)" - vi cho phep nhieu don vi
   * trung ten (vd nhieu "Phong 1" thuoc cac cuc khac nhau), can hien them
   * don vi cha + ma dinh danh de nguoi dung phan biet duoc khi chon trong
   * danh sach phang (dropdown khong the hien cay phan cap). */
  private buildDonViLabel(
    dv: DonViDto,
    byId: Map<number, DonViDto>,
  ): string {
    const parentName =
      dv.parentId != null ? byId.get(dv.parentId)?.tenDonVi : null;
    // Nhet ca ten viet tat vao label de filterBy="label" cua p-dropdown
    // tim duoc theo viet tat (vd go "H05" van ra "Cuc Cong nghe thong tin").
    // Bo ma dinh danh (G01.xxx.xxx) khoi label - qua dai gay tran dong,
    // chi giu ten + viet tat + ten don vi cha la du phan biet.
    const vietTat = dv.tenVietTat ? ` [${dv.tenVietTat}]` : '';
    return parentName
      ? `${dv.tenDonVi}${vietTat} — ${parentName}`
      : `${dv.tenDonVi}${vietTat}`;
  }

  private buildUserRoleOptions(): void {
    this.userRoleOptions = this.roles
      .map((role) => ({
        value: role.id,
        label: role.tenRole || role.roleCode,
      }))
      .sort((a, b) => a.label.localeCompare(b.label));
  }

  async load(): Promise<void> {
    this.loading = true;
    this.apiError = '';

    try {
      switch (this.module) {
        case 'users':
          [this.users, this.donVis] = await Promise.all([
            this.identityApi.getUsers(),
            this.identityApi.getDonVis(),
          ]);
          this.roles = await this.identityApi.getRoles().catch(() => []);
          this.userRoleMappings = await this.identityApi
            .getUserRoleMappings()
            .catch(() => []);
          this.syncDonViOptions();
          this.buildUserDonViOptions();
          this.buildUserRoleOptions();
          break;
        case 'roles':
          this.roles = await this.identityApi.getRoles();
          break;
        case 'permissions':
          this.permissions = await this.identityApi.getPermissions();
          break;
        default:
          await this.loadPermissionManagementData();
          this.initializePermissionManagementState();
          break;
      }
    } catch {
      this.apiError = 'Không thể tải dữ liệu quản trị hệ thống.';
    } finally {
      this.loading = false;
    }
  }

  resolveRoleNames(roleIds: number[]): string {
    if (!roleIds.length) {
      return '-';
    }

    const labels = this.roles
      .filter((role) => roleIds.includes(role.id))
      .map((role) => role.tenRole || role.roleCode);

    return labels.length ? labels.join(', ') : '-';
  }

  resolvePermissionCodes(permissionIds: number[]): string {
    if (!permissionIds.length) {
      return '-';
    }

    const labels = this.permissions
      .filter((permission) => permissionIds.includes(permission.id))
      .map((permission) => permission.permCode);

    return labels.length ? labels.join(', ') : '-';
  }

  get filteredUsers(): UserDto[] {
    const keyword = this.searchTerm.trim().toLowerCase();

    return this.users.filter((user) => {
      const searchMatches =
        !keyword ||
        user.username.toLowerCase().includes(keyword) ||
        user.hoTen.toLowerCase().includes(keyword) ||
        (user.email ?? '').toLowerCase().includes(keyword);

      const donViMatches =
        this.selectedDonViId === null || user.donViId === this.selectedDonViId;

      const statusMatches =
        this.selectedStatus === '' ||
        (this.selectedStatus === 'active' && user.isActive) ||
        (this.selectedStatus === 'inactive' && !user.isActive);

      return searchMatches && donViMatches && statusMatches;
    });
  }

  get pagedSummary(): string {
    if (this.totalUsers === 0) {
      return 'Không có kết quả phù hợp';
    }
    const start = this.first + 1;
    const end = Math.min(this.first + this.rows, this.totalUsers);
    return `Hiển thị ${start}\u2013${end} trong tổng số ${this.totalUsers} người dùng`;
  }

  get filteredRoles(): RoleDto[] {
    const keyword = this.roleSearchTerm.trim().toLowerCase();

    return this.roles.filter((role) => {
      const searchMatches =
        !keyword ||
        role.roleCode.toLowerCase().includes(keyword) ||
        role.tenRole.toLowerCase().includes(keyword);

      const typeMatches =
        this.selectedRoleType === '' ||
        this.resolveRoleTypeKey(role) === this.selectedRoleType;

      const statusMatches =
        this.selectedRoleStatus === '' ||
        (this.selectedRoleStatus === 'active' &&
          this.resolveRoleIsActive(role)) ||
        (this.selectedRoleStatus === 'inactive' &&
          !this.resolveRoleIsActive(role));

      return searchMatches && typeMatches && statusMatches;
    });
  }

  get permissionModuleOptions(): Array<{ label: string; value: string }> {
    const modules = Array.from(
      new Set(
        this.permissions
          .map((item) => (item.module ?? '').trim())
          .filter((item) => item.length > 0),
      ),
    ).sort((a, b) => a.localeCompare(b));

    return [
      { label: 'Tất cả mô-đun', value: '' },
      ...modules.map((moduleKey) => ({
        value: moduleKey,
        label: this.resolvePermissionModuleLabel(moduleKey),
      })),
    ];
  }

  get permissionActionOptions(): Array<{ label: string; value: string }> {
    const actions = Array.from(
      new Set(
        this.permissions
          .map((item) => (item.action ?? '').trim())
          .filter((item) => item.length > 0),
      ),
    ).sort((a, b) => a.localeCompare(b));

    return [
      { label: 'Tất cả hành động', value: '' },
      ...actions.map((actionKey) => ({
        value: actionKey,
        label: this.resolvePermissionActionLabel(actionKey),
      })),
    ];
  }

  get filteredPermissions(): PermissionItemDto[] {
    const keyword = this.appliedPermissionSearch.trim().toLowerCase();
    const selectedModule = this.appliedPermissionModule.trim().toLowerCase();
    const selectedAction = this.appliedPermissionAction.trim().toLowerCase();

    return this.permissions.filter((permission) => {
      const permCode = (permission.permCode ?? '').toLowerCase();
      const module = (permission.module ?? '').toLowerCase();
      const action = (permission.action ?? '').toLowerCase();
      const description = (permission.moTa ?? '').toLowerCase();

      const keywordMatches =
        !keyword ||
        permCode.includes(keyword) ||
        module.includes(keyword) ||
        description.includes(keyword);

      const moduleMatches = !selectedModule || module === selectedModule;
      const actionMatches = !selectedAction || action === selectedAction;

      return keywordMatches && moduleMatches && actionMatches;
    });
  }

  get pagedPermissions(): PermissionItemDto[] {
    return this.filteredPermissions.slice(
      this.permissionFirst,
      this.permissionFirst + this.permissionRows,
    );
  }

  get totalPermissions(): number {
    return this.filteredPermissions.length;
  }

  get permissionPagedSummary(): string {
    if (this.totalPermissions === 0) {
      return 'Không có kết quả phù hợp';
    }

    const start = this.permissionFirst + 1;
    const end = Math.min(
      this.permissionFirst + this.permissionRows,
      this.totalPermissions,
    );
    return `Hiển thị ${start}\u2013${end} trong tổng số ${this.totalPermissions} quyền`;
  }

  get pagedRoles(): RoleDto[] {
    return this.filteredRoles.slice(
      this.roleFirst,
      this.roleFirst + this.roleRows,
    );
  }

  get totalRoles(): number {
    return this.filteredRoles.length;
  }

  get rolePagedSummary(): string {
    if (this.totalRoles === 0) {
      return 'Không có kết quả phù hợp';
    }
    const start = this.roleFirst + 1;
    const end = Math.min(this.roleFirst + this.roleRows, this.totalRoles);
    return `Hiển thị ${start}\u2013${end} trong tổng số ${this.totalRoles} vai trò`;
  }

  get pagedUsers(): UserDto[] {
    return this.filteredUsers.slice(this.first, this.first + this.rows);
  }

  get totalUsers(): number {
    return this.filteredUsers.length;
  }

  getTotalRecordsLabel(): string {
    return `${this.totalUsers} tài khoản`;
  }

  onFilterChange(): void {
    this.first = 0;
  }

  onRoleFilterChange(): void {
    this.roleFirst = 0;
  }

  onRowsChange(): void {
    this.first = 0;
  }

  onPageChange(event: PaginatorState): void {
    this.first = event.first ?? 0;
    this.rows = event.rows ?? this.rows;
  }

  onRolePageChange(event: PaginatorState): void {
    this.roleFirst = event.first ?? 0;
    this.roleRows = event.rows ?? this.roleRows;
  }

  onPermissionApplyFilters(): void {
    this.appliedPermissionSearch = this.permissionSearchInput;
    this.appliedPermissionModule = this.permissionModuleInput;
    this.appliedPermissionAction = this.permissionActionInput;
    this.permissionFirst = 0;
  }

  async onPermissionRefresh(): Promise<void> {
    this.permissionSearchInput = '';
    this.permissionModuleInput = '';
    this.permissionActionInput = '';
    this.appliedPermissionSearch = '';
    this.appliedPermissionModule = '';
    this.appliedPermissionAction = '';
    this.permissionFirst = 0;
    await this.load();
  }

  onPermissionRowsChange(): void {
    this.permissionFirst = 0;
  }

  onPermissionPageChange(event: PaginatorState): void {
    this.permissionFirst = event.first ?? 0;
    this.permissionRows = event.rows ?? this.permissionRows;
  }

  resolveDonViName(donViId: number): string {
    const match = this.donVis.find((dv) => dv.id === donViId);
    return match?.tenDonVi ?? `Đơn vị #${donViId}`;
  }

  calculateStt(indexInPage: number): number {
    return this.first + indexInPage + 1;
  }

  calculateRoleStt(indexInPage: number): number {
    return this.roleFirst + indexInPage + 1;
  }

  calculatePermissionStt(indexInPage: number): number {
    return this.permissionFirst + indexInPage + 1;
  }

  resolveRoleTypeLabel(role: RoleDto): string {
    const key = this.resolveRoleTypeKey(role);
    if (key === 'system') {
      return 'Hệ thống';
    }
    return 'Nghiệp vụ';
  }

  resolveRoleTypeSeverity(role: RoleDto): 'info' | 'secondary' {
    const key = this.resolveRoleTypeKey(role);
    if (key === 'system') {
      return 'info';
    }
    return 'secondary';
  }

  resolveRoleDescription(role: RoleDto): string {
    const normalized = (role.moTa ?? '').trim();
    if (normalized) {
      return normalized;
    }

    const type = this.resolveRoleTypeLabel(role).toLowerCase();
    return `Vai trò ${type} dùng để phân quyền truy cập chức năng trong hệ thống.`;
  }

  resolveRoleIsActive(role: RoleDto): boolean {
    return role.isActive ?? true;
  }

  resolvePermissionActionLabel(action: string): string {
    const key = (action ?? '').trim().toLowerCase();
    if (!key) {
      return '-';
    }

    if (key === 'read') {
      return 'Xem';
    }
    if (key === 'create') {
      return 'Thêm';
    }
    if (key === 'update') {
      return 'Sửa';
    }
    if (key === 'delete') {
      return 'Xóa';
    }
    if (key === 'approve') {
      return 'Phê duyệt';
    }
    if (key === 'submit') {
      return 'Gửi';
    }
    if (key === 'upload') {
      return 'Tải lên';
    }
    if (key === 'export' || key === 'pdf') {
      return 'Xuất';
    }
    if (key === 'admin') {
      return 'Quản trị';
    }
    if (key === 'xac_nhan') {
      return 'Xác nhận';
    }

    return key;
  }

  resolvePermissionActionSeverity(
    action: string,
  ): 'success' | 'info' | 'warning' | 'danger' | 'secondary' {
    const key = (action ?? '').trim().toLowerCase();
    if (key === 'read') {
      return 'info';
    }
    if (key === 'create') {
      return 'success';
    }
    if (key === 'update') {
      return 'warning';
    }
    if (key === 'delete') {
      return 'danger';
    }
    if (key === 'approve' || key === 'submit') {
      return 'secondary';
    }

    return 'secondary';
  }

  resolvePermissionModuleLabel(module: string): string {
    const key = (module ?? '').trim().toLowerCase();
    if (!key) {
      return '-';
    }

    return this.moduleCodeLabelMap[key] ?? key;
  }

  resolvePermissionDescription(permission: PermissionItemDto): string {
    const description = (permission.moTa ?? '').trim();
    return description || '-';
  }

  private parsePermissionDescription(permission: PermissionItemDto): {
    featureLabel: string | null;
    actionLabel: string | null;
  } {
    const description = (permission.moTa ?? '').trim();
    if (!description) {
      return { featureLabel: null, actionLabel: null };
    }

    const separatorIndex = description.indexOf(':');
    if (separatorIndex < 0) {
      return { featureLabel: null, actionLabel: null };
    }

    const featureLabel = description.slice(0, separatorIndex).trim();
    const actionLabel = description.slice(separatorIndex + 1).trim();

    return {
      featureLabel: featureLabel || null,
      actionLabel: actionLabel || null,
    };
  }

  private resolvePermissionFeatureLabel(
    permission: PermissionItemDto,
    fallbackResourceKey: string,
  ): string {
    const parsed = this.parsePermissionDescription(permission);
    if (parsed.featureLabel) {
      return parsed.featureLabel;
    }

    return (
      this.moduleCodeLabelMap[(permission.module ?? '').trim().toLowerCase()] ||
      fallbackResourceKey ||
      'Chung'
    );
  }

  private resolvePermissionDisplayActionLabel(
    permission: PermissionItemDto,
  ): string {
    const parsed = this.parsePermissionDescription(permission);
    if (parsed.actionLabel) {
      return parsed.actionLabel;
    }

    return this.resolvePermissionActionLabel(permission.action);
  }

  get permissionRoleOptions(): Array<{ label: string; value: number }> {
    return this.roles
      .filter((role) => this.resolveRoleIsActive(role))
      .map((role) => ({
        value: role.id,
        label: `${role.tenRole} (${role.roleCode})`,
      }))
      .sort((a, b) => a.label.localeCompare(b.label));
  }

  get permissionUserOptions(): Array<{ label: string; value: number }> {
    return this.users
      .filter((user) => user.isActive)
      .map((user) => ({
        value: user.id,
        label: `${user.hoTen} (${user.username})`,
      }))
      .sort((a, b) => a.label.localeCompare(b.label));
  }

  get selectedPermissionRole(): RoleDto | null {
    if (!this.selectedPermissionRoleId) {
      return null;
    }
    return (
      this.roles.find((role) => role.id === this.selectedPermissionRoleId) ??
      null
    );
  }

  get hasPermissionSelectionChanged(): boolean {
    if (this.selectedPermissionIds.size !== this.originalPermissionIds.size) {
      return true;
    }

    for (const id of this.selectedPermissionIds) {
      if (!this.originalPermissionIds.has(id)) {
        return true;
      }
    }

    return false;
  }

  get systemAdminPermissionId(): number | null {
    return (
      this.permissions.find(
        (permission) =>
          permission.permCode.toLowerCase() ===
          IdentityAdminPage.SYSTEM_ADMIN_PERMISSION_CODE,
      )?.id ?? null
    );
  }

  get selectedRoleHasSystemAdminPermission(): boolean {
    const permissionId = this.systemAdminPermissionId;
    return (
      typeof permissionId === 'number' &&
      this.selectedPermissionIds.has(permissionId)
    );
  }

  get isPermissionMatrixReadOnly(): boolean {
    return (
      !!this.selectedPermissionRole?.isSystem &&
      this.selectedRoleHasSystemAdminPermission
    );
  }

  buildPermissionMatrixModules(): PermissionMatrixModuleGroup[] {
    const groupMap = new Map<
      BusinessGroupKey,
      Map<string, PermissionMatrixRow>
    >();

    for (const permission of this.permissions) {
      const parsed = this.parsePermissionEntry(permission);
      if (!parsed) {
        continue;
      }

      let resourceMap = groupMap.get(parsed.businessGroupKey);
      if (!resourceMap) {
        resourceMap = new Map<string, PermissionMatrixRow>();
        groupMap.set(parsed.businessGroupKey, resourceMap);
      }

      let row = resourceMap.get(parsed.resourceKey);
      if (!row) {
        row = {
          resourceKey: parsed.resourceKey,
          resourceLabel: parsed.resourceLabel,
          businessGroupKey: parsed.businessGroupKey,
          businessGroupLabel: parsed.businessGroupLabel,
          permissionIdByAction: {},
          permCodeByAction: {},
          allowedActions: [],
          selectedActions: [],
          permissionIds: [],
        };
        resourceMap.set(parsed.resourceKey, row);
      }

      row.permissionIdByAction[parsed.action] = parsed.permissionId;
      row.permCodeByAction[parsed.action] = parsed.permCode;

      if (!row.allowedActions.includes(parsed.action)) {
        row.allowedActions.push(parsed.action);
      }
      if (this.selectedPermissionIds.has(parsed.permissionId)) {
        row.selectedActions.push(parsed.action);
      }
      if (!row.permissionIds.includes(parsed.permissionId)) {
        row.permissionIds.push(parsed.permissionId);
      }
    }

    return Array.from(groupMap.entries())
      .map(([businessGroupKey, resourceMap]) => {
        const rows = Array.from(resourceMap.values())
          .map((row) => ({
            ...row,
            allowedActions: [...row.allowedActions].sort(
              (a, b) =>
                this.permissionActionOrder(a) - this.permissionActionOrder(b),
            ),
            selectedActions: [...row.selectedActions].sort(
              (a, b) =>
                this.permissionActionOrder(a) - this.permissionActionOrder(b),
            ),
          }))
          .sort((a, b) => a.resourceLabel.localeCompare(b.resourceLabel));

        const permissionIds = Array.from(
          new Set(rows.flatMap((row) => row.permissionIds)),
        );
        const actionPermissionIds: Partial<
          Record<PermissionActionKey, number[]>
        > = {};
        for (const actionColumn of this.permissionActionColumns) {
          const ids = rows
            .map((row) => row.permissionIdByAction[actionColumn.key])
            .filter((id): id is number => typeof id === 'number');
          if (ids.length) {
            actionPermissionIds[actionColumn.key] = ids;
          }
        }

        return {
          businessGroupKey,
          businessGroupLabel:
            this.resolveBusinessGroupLabelByKey(businessGroupKey),
          rows,
          permissionIds,
          actionPermissionIds,
        } as PermissionMatrixModuleGroup;
      })
      .sort((a, b) => a.businessGroupLabel.localeCompare(b.businessGroupLabel));
  }

  buildFilteredPermissionMatrixModules(): PermissionMatrixModuleGroup[] {
    const keyword = this.rolePermissionSearchTerm.trim().toLowerCase();
    if (!keyword) {
      return this.permissionMatrixModulesState;
    }

    return this.permissionMatrixModulesState
      .map((group) => {
        const rows = group.rows.filter((row) => {
          const textMatches =
            row.resourceLabel.toLowerCase().includes(keyword) ||
            row.resourceKey.toLowerCase().includes(keyword) ||
            group.businessGroupLabel.toLowerCase().includes(keyword) ||
            group.businessGroupKey.toLowerCase().includes(keyword);

          if (textMatches) {
            return true;
          }

          return this.permissionActionColumns.some((actionColumn) => {
            const permCode = row.permCodeByAction[actionColumn.key];
            if (!permCode) {
              return false;
            }

            return (
              actionColumn.label.toLowerCase().includes(keyword) ||
              actionColumn.key.toLowerCase().includes(keyword) ||
              permCode.toLowerCase().includes(keyword)
            );
          });
        });

        if (!rows.length) {
          return null;
        }

        return {
          ...group,
          rows,
          permissionIds: Array.from(
            new Set(rows.flatMap((row) => row.permissionIds)),
          ),
          actionPermissionIds: this.buildActionPermissionIds(rows),
        } as PermissionMatrixModuleGroup;
      })
      .filter((group): group is PermissionMatrixModuleGroup => group !== null);
  }

  get selectedPermissionUser(): UserDto | null {
    if (!this.selectedPermissionUserId) {
      return null;
    }
    return (
      this.users.find((user) => user.id === this.selectedPermissionUserId) ??
      null
    );
  }

  get selectedPermissionUserRoleLabels(): string[] {
    if (!this.selectedPermissionUserId) {
      return [];
    }

    const roleIds =
      this.userRoleMappings.find(
        (mapping) => mapping.userId === this.selectedPermissionUserId,
      )?.roleIds ?? [];

    return this.roles
      .filter((role) => roleIds.includes(role.id))
      .map((role) => role.tenRole || role.roleCode)
      .sort((a, b) => a.localeCompare(b));
  }

  buildSelectedPermissionUserPermissions(): PermissionItemDto[] {
    if (!this.selectedPermissionUserId) {
      return [];
    }

    const roleIds =
      this.userRoleMappings.find(
        (mapping) => mapping.userId === this.selectedPermissionUserId,
      )?.roleIds ?? [];

    const permissionIds = new Set<number>();
    for (const roleId of roleIds) {
      const ids = this.getPermissionIdsForRole(roleId);
      for (const id of ids) {
        permissionIds.add(id);
      }
    }

    return this.permissions
      .filter((permission) => permissionIds.has(permission.id))
      .sort((a, b) => {
        const left = `${a.module}:${a.permCode}`.toLowerCase();
        const right = `${b.module}:${b.permCode}`.toLowerCase();
        return left.localeCompare(right);
      });
  }

  buildSelectedPermissionUserPermissionGroups(): Array<{
    moduleLabel: string;
    permissions: Array<{ action: string; feature: string }>;
  }> {
    const groups = new Map<
      string,
      Array<{ action: string; feature: string }>
    >();

    for (const permission of this.selectedPermissionUserPermissionsState) {
      const resourceKey = this.resolvePermissionResourceKey(
        permission.permCode,
      );
      const businessGroupKey = this.resolveBusinessGroupKey(
        resourceKey,
        permission.module,
      );
      const moduleLabel = this.resolveBusinessGroupLabelByKey(businessGroupKey);
      const existing = groups.get(moduleLabel) ?? [];
      existing.push({
        action: this.resolvePermissionDisplayActionLabel(permission),
        feature: this.resolvePermissionFeatureLabel(permission, resourceKey),
      });
      groups.set(moduleLabel, existing);
    }

    return Array.from(groups.entries())
      .map(([moduleLabel, permissions]) => ({
        moduleLabel,
        permissions: permissions.sort((a, b) =>
          `${a.feature}:${a.action}`.localeCompare(`${b.feature}:${b.action}`),
        ),
      }))
      .sort((a, b) => a.moduleLabel.localeCompare(b.moduleLabel));
  }

  onPermissionAdminTabChange(tab: PermissionAdminTab): void {
    this.permissionAdminTab = tab;
  }

  onPermissionRoleChange(): void {
    this.seedSelectedPermissionsForRole(this.selectedPermissionRoleId);
    this.permissionMatrixModulesState = this.buildPermissionMatrixModules();
    this.refreshPermissionMatrixView();
  }

  onPermissionSearchChange(): void {
    this.refreshPermissionMatrixView();
  }

  setActivePermissionModule(moduleKey: string): void {
    this.activePermissionModuleKey = moduleKey;
    this.syncCurrentPermissionModule();
  }

  onPermissionUserChange(): void {
    this.refreshPermissionUserState();
  }

  getRowCell(
    row: PermissionMatrixRow,
    action: PermissionActionKey,
  ): { permissionId: number; permCode: string } | null {
    const permissionId = row.permissionIdByAction[action];
    const permCode = row.permCodeByAction[action];
    if (typeof permissionId !== 'number' || !permCode) {
      return null;
    }

    return { permissionId, permCode };
  }

  isPermissionChecked(permissionId: number): boolean {
    return this.selectedPermissionIds.has(permissionId);
  }

  isActionAllowed(
    row: PermissionMatrixRow,
    action: PermissionActionKey,
  ): boolean {
    return typeof row.permissionIdByAction[action] === 'number';
  }

  isActionSelected(
    row: PermissionMatrixRow,
    action: PermissionActionKey,
  ): boolean {
    const permissionId = row.permissionIdByAction[action];
    return (
      typeof permissionId === 'number' &&
      this.selectedPermissionIds.has(permissionId)
    );
  }

  isRowChecked(row: PermissionMatrixRow): boolean {
    if (!row.permissionIds.length) {
      return false;
    }
    return row.permissionIds.every((id) => this.selectedPermissionIds.has(id));
  }

  isRowIndeterminate(row: PermissionMatrixRow): boolean {
    if (!row.permissionIds.length) {
      return false;
    }

    const selectedCount = row.permissionIds.filter((id) =>
      this.selectedPermissionIds.has(id),
    ).length;
    return selectedCount > 0 && selectedCount < row.permissionIds.length;
  }

  isModuleChecked(group: PermissionMatrixModuleGroup): boolean {
    if (!group.permissionIds.length) {
      return false;
    }
    return group.permissionIds.every((id) =>
      this.selectedPermissionIds.has(id),
    );
  }

  isModuleIndeterminate(group: PermissionMatrixModuleGroup): boolean {
    if (!group.permissionIds.length) {
      return false;
    }

    const selectedCount = group.permissionIds.filter((id) =>
      this.selectedPermissionIds.has(id),
    ).length;
    return selectedCount > 0 && selectedCount < group.permissionIds.length;
  }

  onPermissionCellToggle(permissionId: number, checked: boolean): void {
    if (this.isPermissionMatrixReadOnly) {
      return;
    }

    if (checked) {
      this.selectedPermissionIds.add(permissionId);
      return;
    }
    this.selectedPermissionIds.delete(permissionId);
  }

  onPermissionActionCellToggle(
    row: PermissionMatrixRow,
    action: PermissionActionKey,
    checked: boolean,
  ): void {
    const permissionId = row.permissionIdByAction[action];
    if (typeof permissionId !== 'number') {
      return;
    }
    this.onPermissionCellToggle(permissionId, checked);
  }

  onPermissionRowToggle(row: PermissionMatrixRow, checked: boolean): void {
    if (this.isPermissionMatrixReadOnly) {
      return;
    }

    for (const permissionId of row.permissionIds) {
      if (checked) {
        this.selectedPermissionIds.add(permissionId);
      } else {
        this.selectedPermissionIds.delete(permissionId);
      }
    }
  }

  onPermissionModuleToggle(
    group: PermissionMatrixModuleGroup,
    checked: boolean,
  ): void {
    if (this.isPermissionMatrixReadOnly) {
      return;
    }

    for (const permissionId of group.permissionIds) {
      if (checked) {
        this.selectedPermissionIds.add(permissionId);
      } else {
        this.selectedPermissionIds.delete(permissionId);
      }
    }
  }

  isActionColumnChecked(action: PermissionActionKey): boolean {
    const rows = this.currentPermissionModuleState?.rows ?? [];
    const eligibleRows = rows.filter((row) =>
      this.isActionAllowed(row, action),
    );
    if (!eligibleRows.length) {
      return false;
    }

    return eligibleRows.every((row) => this.isActionSelected(row, action));
  }

  isActionColumnIndeterminate(action: PermissionActionKey): boolean {
    const rows = this.currentPermissionModuleState?.rows ?? [];
    const eligibleRows = rows.filter((row) =>
      this.isActionAllowed(row, action),
    );
    if (!eligibleRows.length) {
      return false;
    }

    const selectedCount = eligibleRows.filter((row) =>
      this.isActionSelected(row, action),
    ).length;

    return selectedCount > 0 && selectedCount < eligibleRows.length;
  }

  isAllChecked(action: PermissionActionKey): boolean {
    return this.isActionColumnChecked(action);
  }

  isIndeterminate(action: PermissionActionKey): boolean {
    return this.isActionColumnIndeterminate(action);
  }

  onActionColumnToggle(action: PermissionActionKey, checked: boolean): void {
    if (this.isPermissionMatrixReadOnly) {
      return;
    }

    const ids =
      this.currentPermissionModuleState?.actionPermissionIds[action] ?? [];
    for (const id of ids) {
      if (checked) {
        this.selectedPermissionIds.add(id);
      } else {
        this.selectedPermissionIds.delete(id);
      }
    }
  }

  async onSaveRolePermissions(): Promise<void> {
    if (!this.selectedPermissionRoleId) {
      this.notificationService.show(
        'warning',
        'Vui lòng chọn vai trò cần phân quyền.',
      );
      return;
    }

    if (this.isPermissionMatrixReadOnly) {
      this.notificationService.show(
        'warning',
        'Vai trò hệ thống với quyền system:admin đang ở chế độ chỉ đọc.',
      );
      return;
    }

    this.savingRolePermissions = true;
    try {
      const permissionIds = Array.from(this.selectedPermissionIds).sort(
        (a, b) => a - b,
      );
      await this.identityApi.updateRolePermissions(
        this.selectedPermissionRoleId,
        {
          permissionIds,
        } as UpdateRolePermissionsRequest,
      );

      const mappingIndex = this.rolePermissionMappings.findIndex(
        (mapping) => mapping.roleId === this.selectedPermissionRoleId,
      );
      if (mappingIndex >= 0) {
        this.rolePermissionMappings[mappingIndex] = {
          ...this.rolePermissionMappings[mappingIndex],
          permissionIds,
        };
      } else {
        const role = this.selectedPermissionRole;
        this.rolePermissionMappings.push({
          roleId: this.selectedPermissionRoleId,
          roleCode: role?.roleCode ?? '',
          tenRole: role?.tenRole ?? '',
          permissionIds,
        });
      }

      this.originalPermissionIds = new Set(permissionIds);
      this.refreshPermissionUserState();
      this.notificationService.show('success', 'Lưu phân quyền thành công.');
    } catch {
      this.notificationService.show(
        'error',
        'Không thể lưu phân quyền. Vui lòng thử lại.',
      );
    } finally {
      this.savingRolePermissions = false;
    }
  }

  async onRefreshPermissionManagement(): Promise<void> {
    await this.load();
  }

  private initializePermissionManagementState(): void {
    if (!this.roles.length) {
      this.selectedPermissionRoleId = null;
      this.selectedPermissionIds = new Set<number>();
      this.originalPermissionIds = new Set<number>();
      return;
    }

    const firstRole = this.roles[0];
    if (!this.selectedPermissionRoleId) {
      this.selectedPermissionRoleId = firstRole.id;
    }
    if (!this.selectedPermissionUserId && this.users.length) {
      this.selectedPermissionUserId = this.users[0].id;
    }

    this.seedSelectedPermissionsForRole(this.selectedPermissionRoleId);
    this.permissionMatrixModulesState = this.buildPermissionMatrixModules();
    this.refreshPermissionMatrixView();
    this.refreshPermissionUserState();
  }

  private async loadPermissionManagementData(): Promise<void> {
    const results = await Promise.allSettled([
      this.withLoadTimeout(this.identityApi.getRoles()),
      this.withLoadTimeout(this.identityApi.getPermissions()),
      this.withLoadTimeout(this.identityApi.getUsers()),
      this.withLoadTimeout(this.identityApi.getDonVis()),
      this.withLoadTimeout(this.identityApi.getUserRoleMappings()),
      this.withLoadTimeout(this.identityApi.getRolePermissionMappings()),
    ]);

    const [
      rolesResult,
      permissionsResult,
      usersResult,
      donVisResult,
      userRoleMappingsResult,
      rolePermissionMappingsResult,
    ] = results;

    this.roles = this.getSettledValue(rolesResult, [] as RoleDto[]);
    this.permissions = this.getSettledValue(
      permissionsResult,
      [] as PermissionItemDto[],
    );
    this.users = this.getSettledValue(usersResult, [] as UserDto[]);
    this.donVis = this.getSettledValue(donVisResult, [] as DonViDto[]);
    this.userRoleMappings = this.getSettledValue(
      userRoleMappingsResult,
      [] as UserRoleMappingDto[],
    );
    this.rolePermissionMappings = this.getSettledValue(
      rolePermissionMappingsResult,
      [] as RolePermissionMappingDto[],
    );

    if (this.donVis.length) {
      this.syncDonViOptions();
    }
    this.buildUserDonViOptions();
    this.buildUserRoleOptions();

    const hasRequiredData =
      this.roles.length > 0 && this.permissions.length > 0;
    if (!hasRequiredData) {
      throw new Error('permission-management-load-failed');
    }

    const partialFailures = results.filter(
      (result) => result.status === 'rejected',
    ).length;
    if (partialFailures > 0) {
      this.notificationService.show(
        'warning',
        'Một phần dữ liệu phân quyền tải chưa đầy đủ. Màn hình vẫn được mở để tiếp tục thao tác.',
      );
    }
  }

  private withLoadTimeout<T>(
    promise: Promise<T>,
    timeoutMs = IdentityAdminPage.LOAD_TIMEOUT_MS,
  ): Promise<T> {
    return new Promise<T>((resolve, reject) => {
      const timeoutHandle = window.setTimeout(() => {
        reject(new Error('load-timeout'));
      }, timeoutMs);

      promise
        .then((value) => {
          window.clearTimeout(timeoutHandle);
          resolve(value);
        })
        .catch((error) => {
          window.clearTimeout(timeoutHandle);
          reject(error);
        });
    });
  }

  private getSettledValue<T>(result: PromiseSettledResult<T>, fallback: T): T {
    return result.status === 'fulfilled' ? result.value : fallback;
  }

  private seedSelectedPermissionsForRole(roleId: number | null): void {
    if (!roleId) {
      this.selectedPermissionIds = new Set<number>();
      this.originalPermissionIds = new Set<number>();
      return;
    }

    const rolePermissionIds = this.getPermissionIdsForRole(roleId);
    this.selectedPermissionIds = new Set(rolePermissionIds);
    this.originalPermissionIds = new Set(rolePermissionIds);
  }

  private refreshPermissionMatrixView(): void {
    this.filteredPermissionMatrixModulesState =
      this.buildFilteredPermissionMatrixModules();

    const stillExists = this.filteredPermissionMatrixModulesState.some(
      (group) => group.businessGroupKey === this.activePermissionModuleKey,
    );

    if (!stillExists) {
      this.activePermissionModuleKey =
        this.filteredPermissionMatrixModulesState[0]?.businessGroupKey ?? '';
    }

    this.syncCurrentPermissionModule();
  }

  private syncCurrentPermissionModule(): void {
    this.currentPermissionModuleState =
      this.filteredPermissionMatrixModulesState.find(
        (group) => group.businessGroupKey === this.activePermissionModuleKey,
      ) ??
      this.filteredPermissionMatrixModulesState[0] ??
      null;
  }

  private refreshPermissionUserState(): void {
    this.selectedPermissionUserPermissionsState =
      this.buildSelectedPermissionUserPermissions();
    this.selectedPermissionUserPermissionGroupsState =
      this.buildSelectedPermissionUserPermissionGroups();
  }

  private buildSelectedPermissionCodes(permissionIds: number[]): string[] {
    const idSet = new Set(permissionIds);
    return this.permissions
      .filter((permission) => idSet.has(permission.id))
      .map((permission) => permission.permCode)
      .sort((a, b) => a.localeCompare(b));
  }

  private getPermissionIdsForRole(roleId: number): number[] {
    const mapping = this.rolePermissionMappings.find(
      (item) => item.roleId === roleId,
    );
    if (mapping && mapping.permissionIds.length) {
      return [...mapping.permissionIds];
    }

    const role = this.roles.find((item) => item.id === roleId);
    if (!role || !role.permissions.length) {
      return [];
    }

    const rolePermissions = new Set(
      role.permissions.map((code) => code.toLowerCase()),
    );
    return this.permissions
      .filter((permission) =>
        rolePermissions.has(permission.permCode.toLowerCase()),
      )
      .map((permission) => permission.id);
  }

  private toPermissionActionKey(action: string): PermissionActionKey | null {
    const key = (action ?? '').trim().toLowerCase();
    if (
      key === 'read' ||
      key === 'create' ||
      key === 'update' ||
      key === 'delete' ||
      key === 'approve' ||
      key === 'submit'
    ) {
      return key;
    }

    if (
      key === 'upload' ||
      key === 'export' ||
      key === 'pdf' ||
      key === 'admin' ||
      key === 'xac_nhan'
    ) {
      return 'other';
    }

    return null;
  }

  private parsePermissionEntry(
    permission: PermissionItemDto,
  ): ParsedPermissionEntry | null {
    const actionFromPermCode = this.extractActionFromPermCode(
      permission.permCode,
    );
    const action = this.toPermissionActionKey(
      permission.action || actionFromPermCode,
    );
    if (!action) {
      return null;
    }

    const resourceKey = this.resolvePermissionResourceKey(permission.permCode);
    const businessGroupKey = this.resolveBusinessGroupKey(
      resourceKey,
      permission.module,
    );

    return {
      permissionId: permission.id,
      permCode: permission.permCode,
      businessGroupKey,
      businessGroupLabel: this.resolveBusinessGroupLabelByKey(businessGroupKey),
      resourceKey,
      resourceLabel: this.resolvePermissionFeatureLabel(
        permission,
        resourceKey,
      ),
      action,
    };
  }

  private resolvePermissionResourceKey(permCode: string): string {
    const normalized = (permCode ?? '').trim().toLowerCase();
    if (!normalized) {
      return 'tong_quat';
    }

    const segments = normalized
      .split(':')
      .map((segment) => segment.trim())
      .filter((segment) => segment.length > 0);

    if (segments.length <= 1) {
      return segments[0] ?? normalized;
    }

    const tailAction = this.toPermissionActionKey(
      segments[segments.length - 1],
    );
    if (tailAction) {
      return segments.slice(0, -1).join(':');
    }

    return segments[0];
  }

  private extractActionFromPermCode(permCode: string): string {
    const normalized = (permCode ?? '').trim().toLowerCase();
    if (!normalized.includes(':')) {
      return '';
    }

    const tail = normalized.split(':').pop() ?? '';
    return tail.trim();
  }

  private resolveBusinessGroupKey(
    resourceKey: string,
    moduleHint?: string,
  ): BusinessGroupKey {
    const normalized = (resourceKey ?? '').trim().toLowerCase();

    if (this.resourceToBusinessGroupMap[normalized]) {
      return this.resourceToBusinessGroupMap[normalized];
    }

    const hint = (moduleHint ?? '').trim().toLowerCase();
    if (hint.includes('van_ban') || hint.includes('document')) {
      return 'documents';
    }
    if (
      hint.includes('ha_tang') ||
      hint.includes('infrastructure') ||
      normalized.startsWith('camera_') ||
      normalized.includes('thiet_bi')
    ) {
      return 'infrastructure';
    }
    if (hint.includes('nhan_luc') || normalized.includes('nhan_luc')) {
      return 'hr_it';
    }
    if (hint.includes('bao_cao') || normalized.includes('bao_cao')) {
      return 'reports';
    }
    if (
      hint.includes('security') ||
      normalized.includes('attt') ||
      normalized.includes('soc')
    ) {
      return 'security';
    }
    if (
      normalized === 'users' ||
      normalized === 'roles' ||
      normalized === 'permissions' ||
      normalized === 'codes'
    ) {
      return 'system_admin';
    }

    return 'other';
  }

  private resolveBusinessGroupLabelByKey(key: BusinessGroupKey): string {
    return this.businessGroupDefinitions[key]?.label ?? 'Khác';
  }

  private permissionActionOrder(action: PermissionActionKey): number {
    return this.permissionActionColumns.findIndex((x) => x.key === action);
  }

  private buildActionPermissionIds(
    rows: PermissionMatrixRow[],
  ): Partial<Record<PermissionActionKey, number[]>> {
    const result: Partial<Record<PermissionActionKey, number[]>> = {};
    for (const actionColumn of this.permissionActionColumns) {
      const ids = rows
        .map((row) => row.permissionIdByAction[actionColumn.key])
        .filter((id): id is number => typeof id === 'number');
      if (ids.length) {
        result[actionColumn.key] = ids;
      }
    }
    return result;
  }

  onCreateRole(): void {
    this.roleDialogMode = 'create';
    this.roleDialogInitialData = {
      roleCode: '',
      tenRole: '',
      moTa: '',
    };
    this.roleDialogVisible = true;
  }

  onEditRole(role: RoleDto): void {
    this.roleDialogMode = 'edit';
    this.roleDialogInitialData = {
      id: role.id,
      roleCode: role.roleCode,
      tenRole: role.tenRole,
      moTa: role.moTa ?? '',
    };
    this.roleDialogVisible = true;
  }

  goToRolePermissions(role: RoleDto): void {
    void this.router.navigate(['/phan-quyen'], {
      queryParams: { roleId: role.id, roleCode: role.roleCode },
    });
  }

  async onDeleteRole(role: RoleDto): Promise<void> {
    if (role.isSystem) {
      this.notificationService.show(
        'warning',
        'Vai trò hệ thống không thể xóa.',
      );
      return;
    }

    const confirmed = await this.confirmDialog.confirmDelete({
      message: `Xác nhận xóa vai trò ${role.tenRole}?`,
      acceptLabel: 'Xóa vai trò',
      rejectLabel: 'Hủy',
    });

    if (!confirmed) {
      return;
    }

    try {
      await this.identityApi.deleteRole(role.id);
      this.notificationService.show('success', 'Xóa vai trò thành công.');
      await this.load();
    } catch {
      this.notificationService.show(
        'error',
        'Không thể xóa vai trò. Vui lòng kiểm tra vai trò đang được sử dụng.',
      );
    }
  }

  onRoleDialogVisibleChange(visible: boolean): void {
    this.roleDialogVisible = visible;
    if (!visible) {
      this.roleDialogInitialData = null;
    }
  }

  async onRoleDialogSave(payload: RoleUpsertSubmitPayload): Promise<void> {
    this.roleDialogSubmitting = true;
    try {
      if (payload.mode === 'create') {
        await this.identityApi.createRole({
          roleCode: payload.roleCode,
          tenRole: payload.tenRole,
          moTa: payload.moTa ?? null,
        } as CreateRoleRequest);
        this.notificationService.show('success', 'Thêm vai trò thành công.');
      } else {
        if (!payload.id) {
          this.notificationService.show(
            'error',
            'Không xác định được vai trò.',
          );
          return;
        }

        await this.identityApi.updateRole(payload.id, {
          roleCode: payload.roleCode,
          tenRole: payload.tenRole,
          moTa: payload.moTa ?? null,
        } as UpdateRoleRequest);
        this.notificationService.show(
          'success',
          'Cập nhật vai trò thành công.',
        );
      }

      this.roleDialogVisible = false;
      this.roleDialogInitialData = null;
      await this.load();
    } catch {
      this.notificationService.show(
        'error',
        'Không thể lưu thông tin vai trò. Vui lòng kiểm tra lại dữ liệu.',
      );
    } finally {
      this.roleDialogSubmitting = false;
    }
  }

  async onEditUser(user: UserDto): Promise<void> {
    this.userDialogMode = 'edit';
    this.userDialogInitialData = {
      id: user.id,
      username: user.username,
      hoTen: user.hoTen,
      donViId: user.donViId,
      isActive: user.isActive,
      email: user.email ?? null,
      soDienThoai: user.soDienThoai ?? null,
      roleId: this.getSelectedRoleIdForUser(user.id),
      mustChangePassword: user.mustChangePassword,
    };
    this.userDialogVisible = true;
  }

  async onResetPassword(user: UserDto): Promise<void> {
    const confirmed = await this.confirmDialog.confirmResetPassword({
      message: `Xác nhận đặt lại mật khẩu cho người dùng ${user.username}?`,
      acceptLabel: 'Đặt lại',
      rejectLabel: 'Hủy',
    });

    if (!confirmed) {
      return;
    }

    this.notificationService.show(
      'success',
      `Đã gửi yêu cầu đặt lại mật khẩu cho ${user.username}.`,
    );
  }

  async onDeleteUser(user: UserDto): Promise<void> {
    const confirmed = await this.confirmDialog.confirmDeactivate({
      message: `Xác nhận ngừng hoạt động người dùng ${user.username}?`,
      acceptLabel: 'Ngừng hoạt động',
      rejectLabel: 'Hủy',
    });

    if (!confirmed) {
      return;
    }

    this.tableLoading = true;
    try {
      await this.identityApi.updateUser(user.id, {
        hoTen: user.hoTen,
        email: user.email ?? null,
        soDienThoai: user.soDienThoai ?? null,
        isActive: false,
        mustChangePassword: user.mustChangePassword,
      });

      await this.load();
      this.notificationService.show('success', 'Đã ngừng hoạt động tài khoản.');
    } finally {
      this.tableLoading = false;
    }
  }

  onCreateUser(): void {
    this.userDialogMode = 'create';
    this.userDialogInitialData = {
      username: '',
      hoTen: '',
      donViId: null,
      isActive: true,
      email: null,
      soDienThoai: null,
      roleId: null,
      mustChangePassword: false,
    };
    this.userDialogVisible = true;
  }

  onUserDialogVisibleChange(visible: boolean): void {
    this.userDialogVisible = visible;
    if (!visible) {
      this.userDialogInitialData = null;
    }
  }

  async onUserDialogSave(payload: UserUpsertSubmitPayload): Promise<void> {
    this.userDialogSubmitting = true;
    try {
      if (payload.mode === 'create') {
        await this.identityApi.createUser({
          username: payload.username,
          password: payload.password ?? '',
          hoTen: payload.hoTen,
          email: payload.email ?? null,
          soDienThoai: payload.soDienThoai ?? null,
          donViId: payload.donViId,
          roleIds: payload.roleId ? [payload.roleId] : [],
        } as CreateUserRequest);

        this.notificationService.show('success', 'Thêm người dùng thành công.');
      } else {
        if (!payload.id) {
          this.notificationService.show(
            'error',
            'Không xác định được người dùng.',
          );
          return;
        }

        await this.identityApi.updateUser(payload.id, {
          hoTen: payload.hoTen,
          email: payload.email ?? null,
          soDienThoai: payload.soDienThoai ?? null,
          donViId: payload.donViId,
          isActive: payload.isActive,
          mustChangePassword: payload.mustChangePassword,
        });

        if (payload.roleId) {
          await this.identityApi.assignUserRoles(payload.id, {
            roleIds: [payload.roleId],
            donViId: payload.donViId,
          } as AssignRolesRequest);
        }

        this.notificationService.show(
          'success',
          'Cập nhật người dùng thành công.',
        );
      }

      this.userDialogVisible = false;
      this.userDialogInitialData = null;
      await this.load();
    } catch {
      this.notificationService.show(
        'error',
        'Không thể lưu thông tin người dùng.',
      );
    } finally {
      this.userDialogSubmitting = false;
    }
  }

  private syncDonViOptions(): void {
    const byId = new Map(this.donVis.map((dv) => [dv.id, dv]));
    const options = this.donVis
      .filter((dv) => dv.isActive)
      .map((dv) => ({
        value: dv.id,
        label: this.buildDonViLabel(dv, byId),
      }))
      .sort((a, b) => a.label.localeCompare(b.label));

    this.donViOptions.splice(1, this.donViOptions.length - 1, ...options);
  }

  private getSelectedRoleIdForUser(userId: number): number | null {
    const mapping = this.userRoleMappings.find((x) => x.userId === userId);
    if (!mapping || !mapping.roleIds.length) {
      return null;
    }
    return mapping.roleIds[0] ?? null;
  }

  private resolveRoleTypeKey(role: RoleDto): 'system' | 'business' {
    if (role.isSystem) {
      return 'system';
    }

    return 'business';
  }
}
