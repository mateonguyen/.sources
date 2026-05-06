import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { NotificationService } from '../ui/notification.service';

export const apiErrorInterceptor: HttpInterceptorFn = (request, next) => {
  const authService = inject(AuthService);
  const router = inject(Router);
  const notificationService = inject(NotificationService);

  return next(request).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401) {
        const isLoginRequest = request.url.includes('/auth/login');
        const hasActiveSession = !!authService.token();

        if (!isLoginRequest && hasActiveSession) {
          notificationService.show(
            'error',
            'Phiên đăng nhập hết hạn. Vui lòng đăng nhập lại.',
          );
          authService.logout();
        }
      } else if (error.status === 403) {
        notificationService.show(
          'error',
          'Bạn không có quyền thực hiện thao tác này.',
        );
        router.navigate(['/forbidden']);
      } else {
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
