import {
  Directive,
  Input,
  TemplateRef,
  ViewContainerRef,
  effect,
  inject,
} from '@angular/core';
import { AuthService } from '../core/auth/auth.service';

@Directive({
  selector: '[hasPermission]',
  standalone: true,
})
export class HasPermissionDirective {
  private readonly viewContainer = inject(ViewContainerRef);
  private readonly templateRef = inject(TemplateRef<unknown>);
  private readonly authService = inject(AuthService);
  private requiredPermission = '';

  @Input('hasPermission')
  set hasPermission(value: string) {
    this.requiredPermission = value;
    this.render();
  }

  constructor() {
    effect(() => {
      this.authService.profile();
      this.render();
    });
  }

  private render(): void {
    this.viewContainer.clear();
    if (
      this.requiredPermission &&
      this.authService.hasPermission(this.requiredPermission)
    ) {
      this.viewContainer.createEmbeddedView(this.templateRef);
    }
  }
}
