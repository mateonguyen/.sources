import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';

@Component({
  selector: 'app-not-found-page',
  standalone: true,
  imports: [RouterLink, CardModule, ButtonModule],
  template: `
    <section class="system-page">
      <p-card styleClass="app-page-card system-card">
        <h1 class="app-page-title">404 - Not Found</h1>
        <p class="app-page-subtitle">Trang ban tim khong ton tai.</p>
        <a
          pButton
          routerLink="/"
          label="Quay lai dashboard"
          class="mt-3 inline-flex"
        ></a>
      </p-card>
    </section>
  `,
  styles: [
    `
      .system-page {
        min-height: 100vh;
        display: grid;
        place-content: center;
        text-align: center;
        gap: 10px;
      }
    `,
  ],
})
export class NotFoundPage {}
