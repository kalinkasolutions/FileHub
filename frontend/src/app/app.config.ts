import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
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
  ],
};
