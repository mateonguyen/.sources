import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () =>
      import('./features/auth/login.page').then((m) => m.LoginPage),
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./layout/shell.page').then((m) => m.ShellPage),
    children: [
      {
        path: 'trang-chu',
        loadComponent: () =>
          import('./features/home/home.page').then((m) => m.HomePage),
      },
      {
        path: 'tai-khoan/thong-tin',
        loadComponent: () =>
          import('./features/account/account-info.page').then(
            (m) => m.AccountInfoPage,
          ),
      },
      {
        path: 'tai-khoan/doi-mat-khau',
        loadComponent: () =>
          import('./features/account/change-password.page').then(
            (m) => m.ChangePasswordPage,
          ),
      },
      {
        path: 'tai-khoan/cau-hinh',
        loadComponent: () =>
          import('./features/account/system-settings.page').then(
            (m) => m.SystemSettingsPage,
          ),
      },
      {
        path: 'tro-giup',
        loadComponent: () =>
          import('./features/help/help.page').then((m) => m.HelpPage),
      },
      {
        path: 'nguoi-dung',
        data: { identityModule: 'users' },
        loadComponent: () =>
          import('./features/identity-admin/identity-admin.page').then(
            (m) => m.IdentityAdminPage,
          ),
      },
      {
        path: 'vai-tro',
        data: { identityModule: 'roles' },
        loadComponent: () =>
          import('./features/identity-admin/identity-admin.page').then(
            (m) => m.IdentityAdminPage,
          ),
      },
      {
        path: 'quyen',
        data: { identityModule: 'permissions' },
        loadComponent: () =>
          import('./features/identity-admin/identity-admin.page').then(
            (m) => m.IdentityAdminPage,
          ),
      },
      {
        path: 'phan-quyen',
        data: { identityModule: 'phan-quyen' },
        loadComponent: () =>
          import('./features/identity-admin/identity-admin.page').then(
            (m) => m.IdentityAdminPage,
          ),
      },
      {
        path: 'tien-do-tong-hop',
        loadComponent: () =>
          import('./features/tien-do-tong-hop/tien-do-tong-hop.page').then(
            (m) => m.TienDoTongHopPage,
          ),
      },
      {
        path: 'tong-hop-du-lieu',
        loadComponent: () =>
          import('./features/tong-hop-du-lieu/tong-hop-du-lieu.page').then(
            (m) => m.TongHopDuLieuPage,
          ),
      },
      {
        path: 'snapshot',
        loadComponent: () =>
          import('./features/snapshot/snapshot.page').then(
            (m) => m.SnapshotPage,
          ),
      },
      {
        path: 'yeu-cau-bo-sung',
        loadComponent: () =>
          import('./features/yeu-cau-bo-sung/yeu-cau-bo-sung.page').then(
            (m) => m.YeuCauBoSungPage,
          ),
      },
      {
        path: 'nop-bao-cao',
        loadComponent: () =>
          import('./features/nop-bao-cao/nop-bao-cao.page').then(
            (m) => m.NopBaoCaoPage,
          ),
      },
      {
        path: 'tien-do-nop',
        loadComponent: () =>
          import('./features/tien-do-nop/tien-do-nop.page').then(
            (m) => m.TienDoNopPage,
          ),
      },
      {
        path: 'don-vi',
        loadComponent: () =>
          import('./features/don-vi/don-vi.page').then((m) => m.DonViPage),
      },
      {
        path: 'ky-bao-cao',
        loadComponent: () =>
          import('./features/ky-bao-cao/ky-bao-cao.page').then(
            (m) => m.KyBaoCaoPage,
          ),
      },
      {
        path: 'mau-bao-cao',
        loadComponent: () =>
          import('./features/mau-bao-cao/mau-bao-cao.page').then(
            (m) => m.MauBaoCaoPage,
          ),
      },
      {
        path: 'nhan-luc-cntt',
        loadComponent: () =>
          import('./features/nhan-luc-cntt/nhan-luc-cntt.page').then(
            (m) => m.NhanLucCnttPage,
          ),
      },
      {
        path: 'codes',
        loadComponent: () =>
          import('./features/codes/codes.page').then((m) => m.CodesPage),
      },
      {
        path: 'van-ban-qppl',
        data: { moduleKey: 'vanBanQppl' },
        loadComponent: () =>
          import('./features/business-modules/business-modules.page').then(
            (m) => m.BusinessModulesPage,
          ),
      },
      {
        path: 'dao-tao-boi-duong',
        data: { moduleKey: 'daoTaoBoiDuong' },
        loadComponent: () =>
          import('./features/business-modules/business-modules.page').then(
            (m) => m.BusinessModulesPage,
          ),
      },
      {
        path: 'dao-tao-hoc-vien',
        data: { moduleKey: 'daoTaoHocVien' },
        loadComponent: () =>
          import('./features/business-modules/business-modules.page').then(
            (m) => m.BusinessModulesPage,
          ),
      },
      {
        path: 'nang-luc-so',
        data: { moduleKey: 'nangLucSo' },
        loadComponent: () =>
          import('./features/business-modules/business-modules.page').then(
            (m) => m.BusinessModulesPage,
          ),
      },
      {
        path: 'thiet-bi-cntt',
        loadComponent: () =>
          import('./features/thiet-bi-cntt/thiet-bi-cntt.page').then(
            (m) => m.ThietBiCnttPage,
          ),
      },
      {
        path: 'he-thong-thong-tin',
        loadComponent: () =>
          import('./features/he-thong-thong-tin/he-thong-thong-tin.page').then(
            (m) => m.HeThongThongTinPage,
          ),
      },
      {
        path: 'ha-tang-mang',
        loadComponent: () =>
          import('./features/ha-tang-mang/ha-tang-mang.page').then(
            (m) => m.HaTangMangPage,
          ),
      },
      {
        path: 'giam-sat-soc',
        loadComponent: () =>
          import('./features/giam-sat-soc/giam-sat-soc.page').then(
            (m) => m.GiamSatSocPage,
          ),
      },
      {
        path: 'giam-sat-noc',
        loadComponent: () =>
          import('./features/giam-sat-noc/giam-sat-noc.page').then(
            (m) => m.GiamSatNocPage,
          ),
      },
      {
        path: 'attt-httt-van-hanh',
        data: { moduleKey: 'atttHtttVanHanh' },
        loadComponent: () =>
          import('./features/business-modules/business-modules.page').then(
            (m) => m.BusinessModulesPage,
          ),
      },
      {
        path: 'attt-httt-dau-tu',
        data: { moduleKey: 'atttHtttDauTu' },
        loadComponent: () =>
          import('./features/business-modules/business-modules.page').then(
            (m) => m.BusinessModulesPage,
          ),
      },
      {
        path: 'giai-phap-attt',
        loadComponent: () =>
          import('./features/giai-phap-attt/giai-phap-attt.page').then(
            (m) => m.GiaiPhapAtttPage,
          ),
      },
      {
        path: 'camera-quan-ly',
        data: { moduleKey: 'cameraQuanLy' },
        loadComponent: () =>
          import('./features/business-modules/business-modules.page').then(
            (m) => m.BusinessModulesPage,
          ),
      },
      {
        path: 'camera-thuc-trang',
        data: { moduleKey: 'cameraThucTrang' },
        loadComponent: () =>
          import('./features/business-modules/business-modules.page').then(
            (m) => m.BusinessModulesPage,
          ),
      },
      {
        path: '',
        pathMatch: 'full',
        redirectTo: 'trang-chu',
      },
    ],
  },
  {
    path: 'forbidden',
    loadComponent: () =>
      import('./features/system/forbidden.page').then((m) => m.ForbiddenPage),
  },
  {
    path: 'not-found',
    loadComponent: () =>
      import('./features/system/not-found.page').then((m) => m.NotFoundPage),
  },
  {
    path: '**',
    redirectTo: 'not-found',
  },
];
