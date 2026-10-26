import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  provideHttpClientTesting,
  HttpTestingController,
} from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;
  const routerSpy = jasmine.createSpyObj<Router>('Router', ['navigate']);

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: Router, useValue: routerSpy },
        AuthService,
      ],
    });

    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('stores token and profile after login', async () => {
    const loginPromise = service.login('admin', 'Admin@123');

    const request = httpMock.expectOne(
      '/api/v1/auth/login?rememberMe=false',
    );
    request.flush({
      success: true,
      data: {
        accessToken: 'token-1',
        refreshToken: 'refresh-1',
        expiresAt: '2099-01-01T07:00:00',
        user: {
          userId: 1,
          username: 'admin',
          hoTen: 'Admin',
          donViId: 2001,
          roles: ['SYSTEM_ADMIN'],
          permissions: ['system:admin'],
        },
      },
    });

    await loginPromise;
    expect(service.token()).toBe('token-1');
    expect(service.profile()?.username).toBe('admin');
  });

  it('clears local state on logout', async () => {
    localStorage.setItem('thuc_luc_access_token', 'token-2');
    localStorage.setItem(
      'thuc_luc_user_profile',
      JSON.stringify({ username: 'u', permissions: [] }),
    );

    const logoutPromise = service.logout();
    const request = httpMock.expectOne('/api/v1/auth/logout');
    request.flush({ success: true, data: { message: 'OK' } });
    await logoutPromise;

    expect(service.token()).toBeNull();
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/login']);
  });

  it('honors an explicitly assigned business permission regardless of role name', () => {
    service.profile.set({
      userId: 2,
      username: 'manager',
      hoTen: 'Manager',
      donViId: 2001,
      roles: ['QUAN_LY'],
      permissions: ['nhan_luc_cntt:read'],
    });

    expect(service.hasPermission('nhan_luc_cntt:read')).toBeTrue();
    expect(service.hasPermission('nhan_luc_cntt:update')).toBeFalse();
  });

  it('does not let system admin bypass the reporting analytics permission', () => {
    service.profile.set({
      userId: 1,
      username: 'admin',
      hoTen: 'Admin',
      donViId: 2001,
      roles: ['SYSTEM_ADMIN'],
      permissions: ['system:admin'],
    });

    expect(service.hasPermission('bien_dong_bao_cao:read')).toBeFalse();
  });
});
