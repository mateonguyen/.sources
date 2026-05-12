import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { NotificationService } from '../ui/notification.service';

// Prevents duplicate "session expired" toasts when multiple requests fail simultaneously.
let sessionExpiredShown = false;

export const apiErrorInterceptor: HttpInterceptorFn = (request, next) => {
  const authService = inject(AuthService);
  const router = inject(Router);
  const notificationService = inject(NotificationService);

  return next(request).pipe(
    catchError((error: HttpErrorResponse) => {
      // Auth infrastructure endpoints handle their own errors — never show toasts for them.
      // /auth/refresh: handled by authInterceptor + authService.logout()
      // /auth/logout: called fire-and-forget during logout, 401 is expected/benign
      const isSilentEndpoint =
        request.url.includes('/auth/refresh') ||
        request.url.includes('/auth/logout');

      if (error.status === 401) {
        const isAuthEndpoint =
          isSilentEndpoint || request.url.includes('/auth/login');

        if (!isAuthEndpoint && !sessionExpiredShown) {
          sessionExpiredShown = true;
          notificationService.show(
            'error',
            'Phiên đăng nhập hết hạn. Vui lòng đăng nhập lại.',
          );
          // Reset after delay so re-login in the same SPA session works correctly.
          setTimeout(() => {
            sessionExpiredShown = false;
          }, 5000);
        }
      } else if (error.status === 403) {
        notificationService.show(
          'error',
          'Bạn không có quyền thực hiện thao tác này.',
        );
        router.navigate(['/forbidden']);
      } else if (error.status !== 0 && !isSilentEndpoint && authService.isAuthenticated()) {
        // Skip network errors (status 0), silent auth-flow endpoints, and stale errors after logout.
        const responseError = error.error?.error ?? error.error?.Error;
        const message =
          responseError?.message ??
          responseError?.Message ??
          error.error?.message ??
          error.error?.Message ??
          'Có lỗi xảy ra. Vui lòng thử lại.';

        notificationService.show('error', message);
      }

      return throwError(() => error);
    }),
  );
};
