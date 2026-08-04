import { CommonModule } from '@angular/common';
import {
  Component,
  ElementRef,
  HostListener,
  OnDestroy,
  OnInit,
  ViewChild,
} from '@angular/core';
import {
  NavigationEnd,
  Router,
  RouterLink,
  RouterLinkActive,
  RouterOutlet,
} from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { ProgressBarModule } from 'primeng/progressbar';
import { Subscription } from 'rxjs';
import { filter } from 'rxjs/operators';
import { AuthService } from '../core/auth/auth.service';
import { DonViModeService } from '../core/don-vi/don-vi-mode.service';
import { LoadingService } from '../core/ui/loading.service';
import { DonViApi } from '../features/don-vi/don-vi.api';

interface UserMenuItem {
  label: string;
  icon: string;
  action: () => void;
}

interface SidebarMenuItem {
  label: string;
  route: string;
  icon: string;
  permission?: string;
}

interface SidebarMenuGroup {
  id: string;
  label: string;
  items: SidebarMenuItem[];
  /** Ẩn cả nhóm khi đơn vị của user ở chế độ nhập liệu TONG_HOP. */
  hideWhenTongHop?: boolean;
}

@Component({
  selector: 'app-shell-page',
  standalone: true,
  imports: [
    CommonModule,
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    ButtonModule,
    ProgressBarModule,
  ],
  templateUrl: './shell.page.html',
  styleUrl: './shell.page.scss',
})
export class ShellPage implements OnInit, OnDestroy {
  private static readonly EXPANDED_GROUPS_STORAGE_KEY =
    'thuc-luc:sidebar:expanded-groups';

  userMenuOpen = false;

  readonly userMenuItems: UserMenuItem[] = [
    {
      label: 'Thông tin tài khoản',
      icon: 'pi pi-id-card',
      action: () => this.navigateFromUserMenu('/tai-khoan/thong-tin'),
    },
    {
      label: 'Đổi mật khẩu',
      icon: 'pi pi-lock',
      action: () => this.navigateFromUserMenu('/tai-khoan/doi-mat-khau'),
    },
    {
      label: 'Cấu hình hệ thống',
      icon: 'pi pi-cog',
      action: () => this.navigateFromUserMenu('/tai-khoan/cau-hinh'),
    },
    {
      label: 'Trợ giúp',
      icon: 'pi pi-question-circle',
      action: () => this.navigateFromUserMenu('/tro-giup'),
    },
  ];

  readonly menuGroups: SidebarMenuGroup[] = [
    {
      id: 'quan-tri-he-thong',
      label: 'Quản trị hệ thống',
      items: [
        {
          label: 'Người dùng',
          route: '/nguoi-dung',
          icon: 'pi pi-user',
          permission: 'users:read',
        },
        {
          label: 'Vai trò',
          route: '/vai-tro',
          icon: 'pi pi-shield',
          permission: 'roles:read',
        },
        {
          label: 'Quyền',
          route: '/quyen',
          icon: 'pi pi-key',
          permission: 'roles:read',
        },
        {
          label: 'Phân quyền',
          route: '/phan-quyen',
          icon: 'pi pi-link',
          permission: 'roles:update',
        },
        {
          label: 'Đơn vị',
          route: '/don-vi',
          icon: 'pi pi-sitemap',
          permission: 'don_vi:read',
        },
        {
          label: 'Quản lý danh mục',
          route: '/codes',
          icon: 'pi pi-list',
          permission: 'codes:read',
        },
        {
          label: 'Loại thiết bị CNTT',
          route: '/ref-loai-thiet-bi',
          icon: 'pi pi-desktop',
          permission: 'ref_loai_thiet_bi:read',
        },
      ],
    },
    {
      id: 'bao-cao',
      label: 'Báo cáo',
      items: [
        {
          label: 'Tổng hợp dữ liệu',
          route: '/tong-hop-du-lieu',
          icon: 'pi pi-check-circle',
          permission: 'tong_hop_tien_do:xac_nhan',
        },
        {
          label: 'Nộp báo cáo',
          route: '/nop-bao-cao',
          icon: 'pi pi-send',
          permission: 'snapshot:submit',
        },
        {
          label: 'Yêu cầu bổ sung',
          route: '/yeu-cau-bo-sung',
          icon: 'pi pi-exclamation-circle',
          // chỉ vai duyệt (QUAN_LY) mới cần xem danh sách toàn bộ đơn vị ở đây;
          // CAP_TINH/CAP_XA gửi + xem yêu cầu của mình ngay trong màn Nộp báo cáo
          permission: 'yeu_cau_bo_sung:approve',
        },
        {
          label: 'Tiến độ tổng hợp',
          route: '/tien-do-tong-hop',
          icon: 'pi pi-chart-bar',
          permission: 'tong_hop_tien_do:read',
        },
        {
          label: 'Tiến độ báo cáo',
          route: '/tien-do-nop',
          icon: 'pi pi-list-check',
          permission: 'tien_do_bao_cao:read',
        },
        {
          label: 'Kỳ báo cáo',
          route: '/ky-bao-cao',
          icon: 'pi pi-calendar',
          permission: 'ky_bao_cao:create',
        },
        {
          label: 'Mẫu báo cáo',
          route: '/mau-bao-cao',
          icon: 'pi pi-file',
          permission: 'mau_bao_cao:read',
        },
        {
          label: 'Tra cứu báo cáo',
          route: '/tra-cuu-bao-cao',
          icon: 'pi pi-search',
          permission: 'snapshot:read',
        },
      ],
    },
    {
      id: 'nhan-luc-cntt',
      label: 'Nhân lực CNTT',
      hideWhenTongHop: true,
      items: [
        {
          label: 'Nhân lực CNTT',
          route: '/nhan-luc-cntt',
          icon: 'pi pi-users',
          permission: 'nhan_luc_cntt:read',
        },
        {
          label: 'Đào tạo bồi dưỡng',
          route: '/dao-tao-boi-duong',
          icon: 'pi pi-users',
          permission: 'dao_tao_boi_duong:read',
        },
        {
          label: 'Đào tạo học viện',
          route: '/dao-tao-hoc-vien',
          icon: 'pi pi-building',
          permission: 'dao_tao_hoc_vien:read',
        },
        {
          label: 'Năng lực số',
          route: '/nang-luc-so',
          icon: 'pi pi-chart-bar',
          permission: 'nang_luc_so:read',
        },
      ],
    },
    {
      id: 'ha-tang-he-thong',
      label: 'Hạ tầng & hệ thống',
      hideWhenTongHop: true,
      items: [
        {
          label: 'Thiết bị CNTT',
          route: '/thiet-bi-cntt',
          icon: 'pi pi-desktop',
          permission: 'thiet_bi_cntt:read',
        },
        {
          label: 'Hệ thống thông tin',
          route: '/he-thong-thong-tin',
          icon: 'pi pi-server',
          permission: 'he_thong_thong_tin:read',
        },
        {
          label: 'Hạ tầng mạng',
          route: '/ha-tang-mang',
          icon: 'pi pi-wifi',
          permission: 'ha_tang_mang:read',
        },
        {
          label: 'Giám sát NOC',
          route: '/giam-sat-noc',
          icon: 'pi pi-server',
          permission: 'giam_sat_noc:read',
        },
        {
          label: 'Camera quản lý',
          route: '/camera-quan-ly',
          icon: 'pi pi-video',
          permission: 'camera_quan_ly:read',
        },
        {
          label: 'Camera thực trạng',
          route: '/camera-thuc-trang',
          icon: 'pi pi-map-marker',
          permission: 'camera_thuc_trang:read',
        },
        {
          label: 'Dự án CNTT',
          route: '/du-an-cntt',
          icon: 'pi pi-briefcase',
          permission: 'du_an_cntt:read',
        },
      ],
    },
    {
      id: 'van-ban-quan-ly',
      label: 'Văn bản & quản lý',
      hideWhenTongHop: true,
      items: [
        {
          label: 'Văn bản QPPL',
          route: '/van-ban-qppl',
          icon: 'pi pi-book',
          permission: 'van_ban_qppl:read',
        },
      ],
    },
    {
      id: 'an-toan-thong-tin',
      label: 'An toàn thông tin',
      hideWhenTongHop: true,
      items: [
        {
          label: 'Giám sát SOC',
          route: '/giam-sat-soc',
          icon: 'pi pi-shield',
          permission: 'giam_sat_soc:read',
        },
        {
          label: 'ATTT vận hành',
          route: '/attt-httt-van-hanh',
          icon: 'pi pi-lock',
          permission: 'attt_httt_van_hanh:read',
        },
        {
          label: 'ATTT đầu tư',
          route: '/attt-httt-dau-tu',
          icon: 'pi pi-wallet',
          permission: 'attt_httt_dau_tu:read',
        },
        {
          label: 'Giải pháp ATTT',
          route: '/giai-phap-attt',
          icon: 'pi pi-shield',
          permission: 'giai_phap_attt:read',
        },
      ],
    },
  ];

  menuQuery = '';
  userDonViLabel = '';

  private readonly expandedGroupIds = new Set<string>(['quan-tri-he-thong']);
  private readonly searchExpandedGroupIds = new Set<string>();
  private routerEventsSub?: Subscription;

  @ViewChild('menuSearchInput')
  private menuSearchInput?: ElementRef<HTMLInputElement>;

  constructor(
    public readonly authService: AuthService,
    public readonly loadingService: LoadingService,
    private readonly donViApi: DonViApi,
    private readonly donViModeService: DonViModeService,
    private readonly router: Router,
  ) {}

  ngOnInit(): void {
    this.loadExpandedGroupState();
    this.expandGroupForCurrentRoute();
    void this.loadUserDonViLabel();
    void this.donViModeService.ensureLoaded();

    this.routerEventsSub = this.router.events
      .pipe(filter((event) => event instanceof NavigationEnd))
      .subscribe(() => {
        this.closeUserMenu();
        this.expandGroupForCurrentRoute();
      });
  }

  ngOnDestroy(): void {
    this.routerEventsSub?.unsubscribe();
  }

  get filteredMenuGroups(): SidebarMenuGroup[] {
    const isTongHop = this.donViModeService.isTongHop;
    const visibleGroups = this.menuGroups
      .filter((group) => !(isTongHop && group.hideWhenTongHop))
      .map((group) => ({
        ...group,
        items: group.items.filter((item) => this.canView(item)),
      }))
      .filter((group) => group.items.length > 0);

    const keyword = this.normalize(this.menuQuery);
    if (!keyword) {
      return visibleGroups;
    }

    return visibleGroups
      .map((group) => {
        const groupMatches = this.normalize(group.label).includes(keyword);
        if (groupMatches) {
          return group;
        }

        return {
          ...group,
          items: group.items.filter((item) =>
            this.normalize(item.label).includes(keyword),
          ),
        };
      })
      .filter((group) => group.items.length > 0);
  }

  toggleGroup(groupId: string): void {
    if (this.menuQuery.trim()) {
      return;
    }

    const activeGroupId = this.activeGroupId;

    if (this.expandedGroupIds.has(groupId)) {
      if (activeGroupId === groupId) {
        return;
      }

      this.expandedGroupIds.delete(groupId);
      this.saveExpandedGroupState();
      return;
    }

    for (const expandedGroupId of Array.from(this.expandedGroupIds)) {
      if (expandedGroupId !== activeGroupId) {
        this.expandedGroupIds.delete(expandedGroupId);
      }
    }

    this.expandedGroupIds.add(groupId);
    this.saveExpandedGroupState();
  }

  isGroupExpanded(groupId: string): boolean {
    if (this.menuQuery.trim()) {
      return this.searchExpandedGroupIds.has(groupId);
    }

    return this.expandedGroupIds.has(groupId) || this.activeGroupId === groupId;
  }

  hasActiveItem(group: SidebarMenuGroup): boolean {
    return group.id === this.activeGroupId;
  }

  onMenuQueryChange(value: string): void {
    this.menuQuery = value;
    this.syncSearchExpandedGroups();
  }

  clearMenuQuery(): void {
    if (!this.menuQuery) {
      return;
    }

    this.menuQuery = '';
    this.searchExpandedGroupIds.clear();
  }

  isItemRouteActive(route: string): boolean {
    return this.router.url === route || this.router.url.startsWith(`${route}/`);
  }

  trackByGroup(_: number, group: SidebarMenuGroup): string {
    return group.id;
  }

  trackByItem(_: number, item: SidebarMenuItem): string {
    return item.route;
  }

  @HostListener('window:keydown', ['$event'])
  onWindowKeydown(event: KeyboardEvent): void {
    const isSearchShortcut =
      (event.ctrlKey || event.metaKey) && event.key === 'k';
    if (!isSearchShortcut) {
      return;
    }

    event.preventDefault();
    this.focusMenuSearch();
  }

  logout(): void {
    this.authService.logout();
  }

  async navigateFromUserMenu(route: string): Promise<void> {
    this.closeUserMenu();
    await this.router.navigateByUrl(route);
  }

  toggleUserMenu(): void {
    this.userMenuOpen = !this.userMenuOpen;
  }

  closeUserMenu(): void {
    this.userMenuOpen = false;
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (this.userMenuOpen) {
      const target = event.target as HTMLElement;
      if (!target.closest('.sidebar-user-profile')) {
        this.userMenuOpen = false;
      }
    }
  }

  get userDisplayName(): string {
    const p = this.authService.profile();
    return p?.hoTen || p?.username || '';
  }

  get userRole(): string {
    const roles = this.authService.profile()?.roles ?? [];
    return roles[0] ?? '';
  }

  get hasUserMeta(): boolean {
    return !!this.userDonViLabel;
  }

  get userInitials(): string {
    const name = this.userDisplayName;
    if (!name) return '?';
    const parts = name.trim().split(' ');
    return parts.length >= 2
      ? (parts[0][0] + parts[parts.length - 1][0]).toUpperCase()
      : name.substring(0, 2).toUpperCase();
  }

  private canView(item: SidebarMenuItem): boolean {
    return item.permission
      ? this.authService.hasPermission(item.permission)
      : true;
  }

  private get activeGroupId(): string | null {
    return (
      this.menuGroups.find((group) =>
        group.items.some(
          (item) => this.isItemRouteActive(item.route) && this.canView(item),
        ),
      )?.id ?? null
    );
  }

  private expandGroupForCurrentRoute(): void {
    const matchedGroupId = this.activeGroupId;

    if (!matchedGroupId) {
      return;
    }

    if (this.menuQuery.trim()) {
      this.syncSearchExpandedGroups();
      return;
    }

    this.expandedGroupIds.add(matchedGroupId);
    this.saveExpandedGroupState();
  }

  private syncSearchExpandedGroups(): void {
    const keyword = this.normalize(this.menuQuery);
    this.searchExpandedGroupIds.clear();

    if (!keyword) {
      return;
    }

    for (const group of this.filteredMenuGroups) {
      this.searchExpandedGroupIds.add(group.id);
    }
  }

  private focusMenuSearch(): void {
    setTimeout(() => {
      this.menuSearchInput?.nativeElement.focus();
      this.menuSearchInput?.nativeElement.select();
    }, 0);
  }

  private loadExpandedGroupState(): void {
    try {
      const saved = localStorage.getItem(ShellPage.EXPANDED_GROUPS_STORAGE_KEY);
      if (!saved) {
        return;
      }

      const parsed = JSON.parse(saved);
      if (!Array.isArray(parsed)) {
        return;
      }

      this.expandedGroupIds.clear();
      for (const groupId of parsed) {
        if (typeof groupId === 'string') {
          this.expandedGroupIds.add(groupId);
        }
      }
    } catch {
      this.expandedGroupIds.clear();
      this.expandedGroupIds.add('quan-tri-he-thong');
    }
  }

  private async loadUserDonViLabel(): Promise<void> {
    const donViId = this.authService.profile()?.donViId;
    if (!donViId) {
      this.userDonViLabel = '';
      return;
    }

    try {
      const donVi = await this.donViApi.getById(donViId);
      this.userDonViLabel = `${donVi.maDonVi} - ${donVi.tenDonVi}`;
    } catch {
      this.userDonViLabel = '';
    }
  }

  private saveExpandedGroupState(): void {
    try {
      localStorage.setItem(
        ShellPage.EXPANDED_GROUPS_STORAGE_KEY,
        JSON.stringify(Array.from(this.expandedGroupIds)),
      );
    } catch {
      // Ignore storage errors to keep sidebar usable in restricted browsers.
    }
  }

  private normalize(value: string | null | undefined): string {
    return (value ?? '')
      .normalize('NFD')
      .replace(/\p{Diacritic}/gu, '')
      .trim()
      .toLowerCase();
  }
}
