import {
  HttpErrorResponse,
  HttpHandlerFn,
  HttpRequest,
} from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { Router } from '@angular/router';
import { throwError } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { NotificationService } from '../ui/notification.service';
import { apiErrorInterceptor } from './api-error.interceptor';

describe('apiErrorInterceptor', () => {
  it('shows the session-expired toast only once for repeated 401 responses', () => {
    const tokenState = signal<string | null>('token-123');
    const authService = {
      token: tokenState,
      logout: jasmine.createSpy('logout').and.callFake(() => {
        tokenState.set(null);
      }),
    } as Pick<AuthService, 'token' | 'logout'>;

    const router = jasmine.createSpyObj<Router>('Router', ['navigate']);
    const notificationService = jasmine.createSpyObj<NotificationService>(
      'NotificationService',
      ['show'],
    );

    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: authService },
        { provide: Router, useValue: router },
        { provide: NotificationService, useValue: notificationService },
      ],
    });

    const req = new HttpRequest('GET', '/api/secure-resource');
    const next: HttpHandlerFn = () =>
      throwError(() => new HttpErrorResponse({ status: 401 }));

    TestBed.runInInjectionContext(() => {
      apiErrorInterceptor(req, next).subscribe({ error: () => void 0 });
      apiErrorInterceptor(req, next).subscribe({ error: () => void 0 });
    });

    expect(authService.logout).toHaveBeenCalledTimes(1);
    expect(notificationService.show).toHaveBeenCalledTimes(1);
    expect(notificationService.show).toHaveBeenCalledWith(
      'error',
      'Phiên đăng nhập hết hạn. Vui lòng đăng nhập lại.',
    );
  });

  it('does not show session-expired toast for a failed login request', () => {
    const authService = {
      token: signal<string | null>(null),
      logout: jasmine.createSpy('logout'),
    } as Pick<AuthService, 'token' | 'logout'>;

    const router = jasmine.createSpyObj<Router>('Router', ['navigate']);
    const notificationService = jasmine.createSpyObj<NotificationService>(
      'NotificationService',
      ['show'],
    );

    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: authService },
        { provide: Router, useValue: router },
        { provide: NotificationService, useValue: notificationService },
      ],
    });

    const req = new HttpRequest('GET', '/auth/login');
    const next: HttpHandlerFn = () =>
      throwError(() => new HttpErrorResponse({ status: 401 }));

    TestBed.runInInjectionContext(() => {
      apiErrorInterceptor(req, next).subscribe({ error: () => void 0 });
    });

    expect(authService.logout).not.toHaveBeenCalled();
    expect(notificationService.show).not.toHaveBeenCalled();
  });
});
