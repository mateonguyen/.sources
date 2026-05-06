import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { LoginPage } from './login.page';
import { AuthService } from '../../core/auth/auth.service';

describe('LoginPage', () => {
  it('logs in and navigates on submit', async () => {
    const authServiceSpy = jasmine.createSpyObj<AuthService>('AuthService', ['login', 'refreshProfile']);
    authServiceSpy.login.and.resolveTo();
    authServiceSpy.refreshProfile.and.resolveTo();
    const routerSpy = jasmine.createSpyObj<Router>('Router', ['navigate']);
    routerSpy.navigate.and.resolveTo(true);

    TestBed.configureTestingModule({
      imports: [LoginPage],
      providers: [
        { provide: AuthService, useValue: authServiceSpy },
        { provide: Router, useValue: routerSpy },
      ],
    });

    const fixture = TestBed.createComponent(LoginPage);
    fixture.detectChanges();

    await fixture.componentInstance.submit();

    expect(authServiceSpy.login).toHaveBeenCalled();
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/']);
  });
});
