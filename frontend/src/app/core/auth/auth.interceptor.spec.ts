import { HttpHandlerFn, HttpRequest, HttpResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { of } from 'rxjs';
import { AuthService } from './auth.service';
import { authInterceptor } from './auth.interceptor';

describe('authInterceptor', () => {
  it('adds bearer token when available', (done) => {
    TestBed.configureTestingModule({
      providers: [
        {
          provide: AuthService,
          useValue: { token: signal('token-123') },
        },
      ],
    });

    const req = new HttpRequest('GET', '/api/test');
    const next: HttpHandlerFn = (forwarded) => {
      expect(forwarded.headers.get('Authorization')).toBe('Bearer token-123');
      return of(new HttpResponse({ status: 200 }));
    };

    TestBed.runInInjectionContext(() => {
      authInterceptor(req, next).subscribe(() => done());
    });
  });
});
