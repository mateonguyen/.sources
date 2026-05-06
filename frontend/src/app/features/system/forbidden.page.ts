import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';

@Component({
  selector: 'app-forbidden-page',
  standalone: true,
  imports: [RouterLink, CardModule, ButtonModule],
  template: `
    <section class="system-page">
      <p-card styleClass="app-page-card system-card">
        <h1 class="app-page-title">403 - Forbidden</h1>
        <p class="app-page-subtitle">
          Ban khong co quyen truy cap tai nguyen nay.
        </p>
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
export class ForbiddenPage {}
