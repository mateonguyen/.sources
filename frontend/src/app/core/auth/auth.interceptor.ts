import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from './auth.service';
import { catchError, from, switchMap, throwError } from 'rxjs';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const authService = inject(AuthService);
  const token = authService.token();

  if (!token) {
    return next(request);
  }

  // Thêm Authorization header
  request = request.clone({
    setHeaders: {
      Authorization: `Bearer ${token}`,
    },
  });

  return next(request).pipe(
    catchError((error: HttpErrorResponse) => {
      // Nếu nhận 401 (Unauthorized) và đây không phải là login/refresh endpoint, cố gắng refresh token
      if (
        error.status === 401 &&
        !request.url.includes('/auth/login') &&
        !request.url.includes('/auth/refresh')
      ) {
        return from(authService.refreshAccessToken()).pipe(
          switchMap((newToken) =>
            next(
              request.clone({
                setHeaders: {
                  Authorization: `Bearer ${newToken}`,
                },
              }),
            ),
          ),
          catchError((refreshError) => throwError(() => refreshError)),
        );
      }

      return throwError(() => error);
    }),
  );
};
