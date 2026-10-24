import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '@services/auth.service';
import { map } from 'rxjs';

/**
 * Everything but the share links and the mail-link screens sits behind this. Only a server that
 * says "signed out" sends the caller to sign in; when it could not be asked, the page loads and its
 * own calls find out.
 */
export const authGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  return authService
    .ensureLoaded()
    .pipe(
      map((status) => (status && !status.authenticated ? router.createUrlTree(['/login']) : true)),
    );
};
