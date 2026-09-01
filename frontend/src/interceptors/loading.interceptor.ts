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
 * Reads do not raise it anyway (see below); the token is for a *write* nobody is waiting on, and
 * for a read that would otherwise be caught by some future change to the rule.
 *
 * A token rather than a URL test in the interceptor, so the decision sits with the call that knows
 * why it is being made.
 */
export const skipLoadingOverlay = new HttpContextToken<boolean>(() => false);

/**
 * The methods that change something. These are the requests a person pressed a button for and is
 * now waiting on, and the ones where a second press before the answer arrives would do the thing
 * twice — so blocking the screen is the point of the overlay rather than a side effect of it.
 */
const writeMethods = new Set(['POST', 'PUT', 'PATCH', 'DELETE']);

/**
 * The full-screen overlay, raised for writes only.
 *
 * It used to go up for every request, which meant two loading indicators on screen at once
 * wherever a screen also has one of its own: opening a folder drew "Loading…" in the listing panel
 * *and* a blocking modal spinner on top of it. A read is what a screen was opened to do, and the
 * screen is the right place to say it is doing it — the listing keeps its own row, the admin
 * sections keep their `@defer` placeholders, and the log screen deliberately keeps neither.
 *
 * What the overlay is actually for is the other half: a save the user must not press twice, and
 * must not navigate away from mid-flight.
 */
export const loadingInterceptor: HttpInterceptorFn = (
  req: HttpRequest<unknown>,
  next: HttpHandlerFn,
) => {
  if (!writeMethods.has(req.method)) {
    return next(req);
  }

  if (req.context.get(skipLoadingOverlay)) {
    return next(req);
  }

  const loadingService = inject(LoadingService);
  loadingService.show();
  return next(req).pipe(finalize(() => loadingService.hide()));
};
