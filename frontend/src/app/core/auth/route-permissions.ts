/**
 * Nguon quyen duy nhat cho dieu huong nghiep vu.
 * Sidebar, truy cap nhanh va route guard deu phai doc tu bang nay de tranh
 * truong hop an menu nhung van mo duoc URL (hoac nguoc lai).
 */
export const APP_ROUTE_PERMISSIONS: Readonly<Record<string, string>> = {
  '/nguoi-dung': 'users:read',
  '/vai-tro': 'roles:read',
  '/quyen': 'permissions:read',
  '/phan-quyen': 'phan_quyen:read',
  '/don-vi': 'don_vi:read',
  '/codes': 'codes:read',
  '/ref-loai-thiet-bi': 'ref_loai_thiet_bi:read',
  '/tong-hop-du-lieu': 'tong_hop_tien_do:xac_nhan',
  '/nop-bao-cao': 'snapshot:submit',
  '/yeu-cau-bo-sung': 'yeu_cau_bo_sung:approve',
  '/tien-do-nop': 'tien_do_bao_cao:read',
  '/ky-bao-cao': 'ky_bao_cao:read',
  '/mau-bao-cao': 'mau_bao_cao:read',
  '/tra-cuu-bao-cao': 'snapshot:read',
  '/so-sanh-bao-cao': 'bien_dong_bao_cao:read',
  '/nhan-luc-cntt': 'nhan_luc_cntt:read',
  '/dao-tao-boi-duong': 'dao_tao_boi_duong:read',
  '/dao-tao-hoc-vien': 'dao_tao_hoc_vien:read',
  '/nang-luc-so': 'nang_luc_so:read',
  '/thiet-bi-cntt': 'thiet_bi_cntt:read',
  '/he-thong-thong-tin': 'he_thong_thong_tin:read',
  '/ha-tang-mang': 'ha_tang_mang:read',
  '/giam-sat-noc': 'giam_sat_noc:read',
  '/camera-quan-ly': 'camera_quan_ly:read',
  '/camera-thuc-trang': 'camera_thuc_trang:read',
  '/du-an-cntt': 'du_an_cntt:read',
  '/van-ban-qppl': 'van_ban_qppl:read',
  '/giam-sat-soc': 'giam_sat_soc:read',
  '/attt-httt-van-hanh': 'attt_httt_van_hanh:read',
  '/attt-httt-dau-tu': 'attt_httt_dau_tu:read',
  '/giai-phap-attt': 'giai_phap_attt:read',
};

/**
 * Cac route ca nhan/dung chung chi can dang nhap, khong thuoc ma tran quyen
 * nghiep vu. Moi route khac khong co trong APP_ROUTE_PERMISSIONS se bi chan
 * theo nguyen tac fail-closed.
 */
export const APP_PERMISSION_EXEMPT_ROUTES: readonly string[] = [
  '/',
  '/trang-chu',
  '/tai-khoan/thong-tin',
  '/tai-khoan/doi-mat-khau',
  '/tai-khoan/cau-hinh',
  '/tro-giup',
];

export function getRoutePermission(url: string): string | null {
  const path = normalizeRoutePath(url);
  const matchedRoute = Object.keys(APP_ROUTE_PERMISSIONS)
    .sort((a, b) => b.length - a.length)
    .find((route) => path === route || path.startsWith(`${route}/`));

  return matchedRoute ? APP_ROUTE_PERMISSIONS[matchedRoute] : null;
}

export function isPermissionExemptRoute(url: string): boolean {
  const path = normalizeRoutePath(url);
  return APP_PERMISSION_EXEMPT_ROUTES.some(
    (route) => path === route || (route !== '/' && path.startsWith(`${route}/`)),
  );
}

function normalizeRoutePath(url: string): string {
  const path = (url || '/').split(/[?#]/, 1)[0];
  if (path === '/') {
    return path;
  }

  return `/${path.replace(/^\/+|\/+$/g, '')}`;
}
