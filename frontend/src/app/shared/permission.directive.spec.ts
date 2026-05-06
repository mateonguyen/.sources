import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { HasPermissionDirective } from './permission.directive';
import { AuthService } from '../core/auth/auth.service';

@Component({
  standalone: true,
  imports: [HasPermissionDirective],
  template: '<div *hasPermission="\'perm:read\'">visible</div>',
})
class HostComponent {}

describe('HasPermissionDirective', () => {
  it('renders content when permission exists', () => {
    TestBed.configureTestingModule({
      imports: [HostComponent],
      providers: [
        {
          provide: AuthService,
          useValue: {
            profile: signal({ permissions: ['perm:read'] }),
            hasPermission: () => true,
          },
        },
      ],
    });

    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('visible');
  });
});
