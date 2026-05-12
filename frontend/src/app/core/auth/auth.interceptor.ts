import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from './auth.service';
import { catchError, from, switchMap, throwError } from 'rxjs';

// Shared across all concurrent requests — ensures only ONE refresh call at a time.
let refreshing$: Promise<string> | null = null;

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const authService = inject(AuthService);
  const token = authService.token();

  if (!token) {
    return next(request);
  }

  request = request.clone({
    setHeaders: { Authorization: `Bearer ${token}` },
  });

  return next(request).pipe(
    catchError((error: HttpErrorResponse) => {
      if (
        error.status === 401 &&
        !request.url.includes('/auth/login') &&
        !request.url.includes('/auth/refresh') &&
        authService.isAuthenticated()
      ) {
        if (!refreshing$) {
          refreshing$ = authService.refreshAccessToken().finally(() => {
            refreshing$ = null;
          });
        }

        return from(refreshing$).pipe(
          switchMap((newToken) =>
            next(
              request.clone({
                setHeaders: { Authorization: `Bearer ${newToken}` },
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
