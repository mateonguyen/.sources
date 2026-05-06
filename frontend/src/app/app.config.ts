import { ApplicationConfig } from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { ConfirmationService, MessageService } from 'primeng/api';
import { authInterceptor } from './core/auth/auth.interceptor';
import { loadingInterceptor } from './core/http/loading.interceptor';
import { apiErrorInterceptor } from './core/http/api-error.interceptor';

import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes),
    provideAnimationsAsync(),
    MessageService,
    ConfirmationService,
    provideHttpClient(
      withInterceptors([
        loadingInterceptor,
        authInterceptor,
        apiErrorInterceptor,
      ]),
    ),
  ],
};
