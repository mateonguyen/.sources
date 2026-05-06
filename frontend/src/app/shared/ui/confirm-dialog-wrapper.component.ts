import { Component } from '@angular/core';
import { ConfirmDialogModule } from 'primeng/confirmdialog';

@Component({
  selector: 'app-confirm-dialog-wrapper',
  standalone: true,
  imports: [ConfirmDialogModule],
  template: `
    <p-confirmDialog
      styleClass="app-confirm-dialog"
      [style]="{ width: '28rem' }"
    ></p-confirmDialog>
  `,
})
export class ConfirmDialogWrapperComponent {}
