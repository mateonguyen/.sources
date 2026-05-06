import { Component } from '@angular/core';
import { ToastModule } from 'primeng/toast';

@Component({
  selector: 'app-toast-outlet',
  standalone: true,
  imports: [ToastModule],
  template: `
    <p-toast
      position="top-right"
      styleClass="app-toast"
      showTransformOptions="translateX(110%)"
      hideTransformOptions="translateX(110%)"
      showTransitionOptions="260ms cubic-bezier(0.16, 1, 0.3, 1)"
      hideTransitionOptions="180ms ease-in"
    ></p-toast>
  `,
  styles: [],
})
export class ToastOutletComponent {}
