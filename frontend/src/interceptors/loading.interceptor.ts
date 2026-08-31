import {
  HttpContextToken,
  HttpHandlerFn,
  HttpInterceptorFn,
  HttpRequest,
} from '@angular/common/http';
import { inject } from '@angular/core';
import { LoadingService } from '@services/loading.service';
import { finalize } from 'rxjs';

/**
 * Set on a request that should not raise the full-screen overlay.
 *
 * The overlay is right for a request a person just triggered and is waiting on — saving a form,
 * opening a screen. It is wrong for a request nobody asked for: the admin log screen re-reads the
 * log whenever the server says a line was written, so on a busy installation the overlay would be
 * up more often than not, over a screen whose whole job is to keep showing you something.
 *
 * A token rather than a URL test in the interceptor, so the decision sits with the call that knows
 * why it is being made.
 */
export const skipLoadingOverlay = new HttpContextToken<boolean>(() => false);

export const loadingInterceptor: HttpInterceptorFn = (
  req: HttpRequest<unknown>,
  next: HttpHandlerFn,
) => {
  if (req.context.get(skipLoadingOverlay)) {
    return next(req);
  }

  const loadingService = inject(LoadingService);
  loadingService.show();
  return next(req).pipe(finalize(() => loadingService.hide()));
};
