import {
  ApplicationConfig,
  inject,
  isDevMode,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { provideServiceWorker } from '@angular/service-worker';
import { AppUpdateService } from '@services/app-update.service';
import { provideToastr } from 'ngx-toastr';

import { routes } from './app.routes';
import { credentialsInterceptor } from '@interceptors/credentials.interceptor';
import { loadingInterceptor } from '@interceptors/loading.interceptor';
import { unauthorizedInterceptor } from '@interceptors/unauthorized.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    // Order is the order they wrap the request in: the cookie goes on first, the loading overlay
    // counts every call, and the 401 handler sits innermost so it sees the response first.
    provideHttpClient(
      withFetch(),
      withInterceptors([credentialsInterceptor, loadingInterceptor, unauthorizedInterceptor]),
    ),
    provideToastr(),
    provideAnimationsAsync(),
    // The worker is only built into a production bundle, so `isDevMode()` and the missing file
    // agree — but registering it in the dev loop would also mean a cached shell served in front of
    // `npm run watch`, which is the one thing that would make live reload lie.
    //
    // `registerWhenStable:30000` keeps registration off the critical path: the browser downloads
    // and installs the worker once the app has gone idle, or after 30s if it never does.
    provideServiceWorker('ngsw-worker.js', {
      enabled: !isDevMode(),
      registrationStrategy: 'registerWhenStable:30000',
    }),
    provideAppInitializer(() => inject(AppUpdateService).start()),
  ],
};
