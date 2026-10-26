import { inject } from '@angular/core';
import { CanActivateChildFn, Router } from '@angular/router';
import { AuthService } from './auth.service';
import {
  getRoutePermission,
  isPermissionExemptRoute,
} from './route-permissions';

export const permissionGuard: CanActivateChildFn = (_route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);
  const requiredPermission = getRoutePermission(state.url);

  if (isPermissionExemptRoute(state.url)) {
    return true;
  }

  if (requiredPermission && authService.hasPermission(requiredPermission)) {
    return true;
  }

  return router.createUrlTree(['/trang-chu']);
};
