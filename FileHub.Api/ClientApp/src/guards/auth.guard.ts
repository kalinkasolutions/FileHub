import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '@services/auth.service';
import { map } from 'rxjs';

/** Everything but the share links and the mail-link screens sits behind this. */
export const authGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  return authService
    .ensureLoaded()
    .pipe(map((status) => (status.authenticated ? true : router.createUrlTree(['/login']))));
};
