import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '@services/auth.service';
import { catchError, throwError } from 'rxjs';

/**
 * The single place a dead session is handled. The API answers an unauthenticated call with 401 and
 * never with a redirect, so nothing else notices that the cookie expired: this drops the cached
 * status — otherwise the guards would keep waving the user through on it — and sends them to the
 * sign-in screen. A 403 is left alone: the caller is signed in, just not allowed.
 */
export const unauthorizedInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  return next(req).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && error.status === 401) {
        authService.invalidate();
        if (!router.url.startsWith('/login')) {
          router.navigate(['/login']);
        }
      }

      return throwError(() => error);
    }),
  );
};
