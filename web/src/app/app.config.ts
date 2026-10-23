import {
  type ApplicationConfig,
  isDevMode,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideServiceWorker } from '@angular/service-worker';
import { routes } from './app.routes';
import { provideAuth } from './core/auth/infrastructure/provide-auth';
import { provideConnection } from './core/connection/infrastructure/provide-connection';
import { provideTheme } from './core/theme/infrastructure/provide-theme';
import { provideAdmin } from './features/admin/infrastructure/provide-admin';
import { provideDeck } from './features/deck/infrastructure/provide-deck';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideAuth(),
    provideConnection(),
    provideTheme(),
    provideDeck(),
    provideAdmin(),
    provideServiceWorker('ngsw-worker.js', {
      enabled: !isDevMode(),
      registrationStrategy: 'registerWhenStable:30000',
    }),
  ],
};
