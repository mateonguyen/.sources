import {
  APP_ROUTE_PERMISSIONS,
  getRoutePermission,
  isPermissionExemptRoute,
} from './route-permissions';

describe('route permissions', () => {
  it('uses dedicated permissions for permission administration routes', () => {
    expect(APP_ROUTE_PERMISSIONS['/quyen']).toBe('permissions:read');
    expect(APP_ROUTE_PERMISSIONS['/phan-quyen']).toBe('phan_quyen:read');
  });

  it('keeps the two catalog menus independently assignable', () => {
    expect(APP_ROUTE_PERMISSIONS['/codes']).toBe('codes:read');
    expect(APP_ROUTE_PERMISSIONS['/ref-loai-thiet-bi']).toBe(
      'ref_loai_thiet_bi:read',
    );
  });

  it('uses view permission to enter the reporting-period screen', () => {
    expect(APP_ROUTE_PERMISSIONS['/ky-bao-cao']).toBe('ky_bao_cao:read');
  });

  it('uses the dedicated change-analysis permission for the analytics screen', () => {
    expect(APP_ROUTE_PERMISSIONS['/so-sanh-bao-cao']).toBe(
      'bien_dong_bao_cao:read',
    );
  });

  it('resolves nested URLs and ignores query strings', () => {
    expect(getRoutePermission('/nguoi-dung/12?tab=roles')).toBe('users:read');
  });

  it('does not require a business permission for account routes', () => {
    expect(getRoutePermission('/tai-khoan/thong-tin')).toBeNull();
    expect(isPermissionExemptRoute('/tai-khoan/thong-tin')).toBeTrue();
  });

  it('does not exempt an unknown business route', () => {
    expect(getRoutePermission('/chuc-nang-chua-khai-bao')).toBeNull();
    expect(isPermissionExemptRoute('/chuc-nang-chua-khai-bao')).toBeFalse();
  });
});
