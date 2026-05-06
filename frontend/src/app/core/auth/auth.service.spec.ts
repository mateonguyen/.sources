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
      'http://localhost:5283/api/v1/auth/login',
    );
    request.flush({
      success: true,
      data: {
        accessToken: 'token-1',
        refreshToken: 'refresh-1',
        expiresAt: '2026-01-01T07:00:00',
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

  it('clears local state on logout', () => {
    localStorage.setItem('thuc_luc_access_token', 'token-2');
    localStorage.setItem(
      'thuc_luc_user_profile',
      JSON.stringify({ username: 'u', permissions: [] }),
    );

    service.logout();

    expect(service.token()).toBeNull();
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/login']);
  });
});
