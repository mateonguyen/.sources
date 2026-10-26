import { CommonModule } from '@angular/common';
import {
  AfterViewInit,
  Component,
  ElementRef,
  HostListener,
  NgZone,
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
import { OverlayScrollbars } from 'overlayscrollbars';
import { Subscription } from 'rxjs';
import { filter } from 'rxjs/operators';
import { AuthService } from '../core/auth/auth.service';
import { getRoutePermission } from '../core/auth/route-permissions';
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
export class ShellPage implements OnInit, AfterViewInit, OnDestroy {
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
        },
        {
          label: 'Vai trò',
          route: '/vai-tro',
          icon: 'pi pi-shield',
        },
        {
          label: 'Quyền',
          route: '/quyen',
          icon: 'pi pi-key',
        },
        {
          label: 'Phân quyền',
          route: '/phan-quyen',
          icon: 'pi pi-link',
        },
        {
          label: 'Đơn vị',
          route: '/don-vi',
          icon: 'pi pi-sitemap',
        },
        {
          label: 'Danh mục chung',
          route: '/codes',
          icon: 'pi pi-list',
        },
        {
          label: 'Loại thiết bị CNTT',
          route: '/ref-loai-thiet-bi',
          icon: 'pi pi-desktop',
        },
      ],
    },
    {
      id: 'bao-cao',
      label: 'Báo cáo',
      items: [
        {
          label: 'Mẫu báo cáo',
          route: '/mau-bao-cao',
          icon: 'pi pi-file',
        },
        {
          label: 'Kỳ báo cáo',
          route: '/ky-bao-cao',
          icon: 'pi pi-calendar',
        },
        {
          label: 'Tổng hợp dữ liệu',
          route: '/tong-hop-du-lieu',
          icon: 'pi pi-check-circle',
        },
        {
          label: 'Nộp báo cáo',
          route: '/nop-bao-cao',
          icon: 'pi pi-send',
        },
        {
          label: 'Tiến độ báo cáo',
          route: '/tien-do-nop',
          icon: 'pi pi-list-check',
        },
        {
          label: 'Tra cứu báo cáo',
          route: '/tra-cuu-bao-cao',
          icon: 'pi pi-search',
        },
        {
          label: 'Hiện trạng & biến động',
          route: '/so-sanh-bao-cao',
          icon: 'pi pi-chart-line',
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
        },
        {
          label: 'Đào tạo bồi dưỡng',
          route: '/dao-tao-boi-duong',
          icon: 'pi pi-users',
        },
        {
          label: 'Đào tạo học viện',
          route: '/dao-tao-hoc-vien',
          icon: 'pi pi-building',
        },
        {
          label: 'Năng lực số',
          route: '/nang-luc-so',
          icon: 'pi pi-chart-bar',
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
        },
        {
          label: 'Hệ thống thông tin',
          route: '/he-thong-thong-tin',
          icon: 'pi pi-server',
        },
        {
          label: 'Hạ tầng mạng',
          route: '/ha-tang-mang',
          icon: 'pi pi-wifi',
        },
        {
          label: 'Giám sát NOC',
          route: '/giam-sat-noc',
          icon: 'pi pi-server',
        },
        {
          label: 'Camera quản lý',
          route: '/camera-quan-ly',
          icon: 'pi pi-video',
        },
        {
          label: 'Camera thực trạng',
          route: '/camera-thuc-trang',
          icon: 'pi pi-map-marker',
        },
        {
          label: 'Dự án CNTT',
          route: '/du-an-cntt',
          icon: 'pi pi-briefcase',
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
        },
        {
          label: 'ATTT vận hành',
          route: '/attt-httt-van-hanh',
          icon: 'pi pi-lock',
        },
        {
          label: 'ATTT đầu tư',
          route: '/attt-httt-dau-tu',
          icon: 'pi pi-wallet',
        },
        {
          label: 'Giải pháp ATTT',
          route: '/giai-phap-attt',
          icon: 'pi pi-shield',
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

  @ViewChild('contentEl')
  private contentEl?: ElementRef<HTMLElement>;

  @ViewChild('sidebarMenuEl')
  private sidebarMenuEl?: ElementRef<HTMLElement>;

  private contentScrollbars?: OverlayScrollbars;
  private sidebarScrollbars?: OverlayScrollbars;

  constructor(
    public readonly authService: AuthService,
    public readonly loadingService: LoadingService,
    private readonly donViApi: DonViApi,
    private readonly donViModeService: DonViModeService,
    private readonly router: Router,
    private readonly ngZone: NgZone,
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

  ngAfterViewInit(): void {
    // Duoi 980px sidebar tro lai position:static (xem @media trong
    // shell.page.scss) va content cuon tu nhien theo trang - khong quan ly
    // bang OverlayScrollbars o breakpoint nay.
    if (!window.matchMedia('(min-width: 981px)').matches) {
      return;
    }

    // Scrollbar dang overlay - an mac dinh, chi hien mo khi di chuot/cuon vao
    // vung tuong ung, khong chiem cho bo cuc (giong scrollbar overlay cua macOS).
    const scrollbarOptions = {
      scrollbars: {
        theme: 'os-theme-dark',
        autoHide: 'move',
        autoHideDelay: 600,
      },
    } as const;

    // OverlayScrollbars tu dung ResizeObserver/MutationObserver rieng, khong
    // lien quan gi den state cua Angular - khoi tao ngoai NgZone de tranh cac
    // callback noi bo cua no kich hoat change detection lien tuc (co the gay
    // vong lap "unstable zone" lam treo trang khi dieu huong).
    this.ngZone.runOutsideAngular(() => {
      if (this.contentEl) {
        this.contentScrollbars = OverlayScrollbars(
          this.contentEl!.nativeElement,
          scrollbarOptions,
        );
      }
      if (this.sidebarMenuEl) {
        this.sidebarScrollbars = OverlayScrollbars(
          this.sidebarMenuEl!.nativeElement,
          scrollbarOptions,
        );
      }
    });
  }

  ngOnDestroy(): void {
    this.routerEventsSub?.unsubscribe();
    this.contentScrollbars?.destroy();
    this.sidebarScrollbars?.destroy();
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
    const requiredPermission = getRoutePermission(item.route);
    return (
      !!requiredPermission && this.authService.hasPermission(requiredPermission)
    );
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
