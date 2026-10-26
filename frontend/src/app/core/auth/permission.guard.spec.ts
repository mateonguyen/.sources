import { TestBed } from '@angular/core/testing';
import { Router, UrlTree } from '@angular/router';
import { AuthService } from './auth.service';
import { permissionGuard } from './permission.guard';

describe('permissionGuard', () => {
  const runGuard = (url: string) =>
    TestBed.runInInjectionContext(() =>
      permissionGuard({} as never, { url } as never),
    );

  it('allows the route when the account has its required permission', () => {
    TestBed.configureTestingModule({
      providers: [
        {
          provide: AuthService,
          useValue: { hasPermission: (permission: string) => permission === 'users:read' },
        },
        { provide: Router, useValue: jasmine.createSpyObj<Router>('Router', ['createUrlTree']) },
      ],
    });

    expect(runGuard('/nguoi-dung')).toBeTrue();
  });

  it('redirects a manager without permission away from permission assignment', () => {
    const redirect = {} as UrlTree;
    const router = jasmine.createSpyObj<Router>('Router', ['createUrlTree']);
    router.createUrlTree.and.returnValue(redirect);
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: { hasPermission: () => false } },
        { provide: Router, useValue: router },
      ],
    });

    expect(runGuard('/phan-quyen')).toBe(redirect);
    expect(router.createUrlTree).toHaveBeenCalledWith(['/trang-chu']);
  });

  it('allows routes that do not participate in the permission matrix', () => {
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: { hasPermission: () => false } },
        { provide: Router, useValue: jasmine.createSpyObj<Router>('Router', ['createUrlTree']) },
      ],
    });

    expect(runGuard('/tai-khoan/doi-mat-khau')).toBeTrue();
  });

  it('denies an unmapped business route by default', () => {
    const redirect = {} as UrlTree;
    const router = jasmine.createSpyObj<Router>('Router', ['createUrlTree']);
    router.createUrlTree.and.returnValue(redirect);
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: { hasPermission: () => true } },
        { provide: Router, useValue: router },
      ],
    });

    expect(runGuard('/chuc-nang-chua-khai-bao')).toBe(redirect);
    expect(router.createUrlTree).toHaveBeenCalledWith(['/trang-chu']);
  });
});
