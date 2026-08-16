import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '@services/auth.service';
import { map } from 'rxjs';

/**
 * An account whose password was set by someone else — an invitation, or an admin reset — carries
 * `mustChangePassword` until it chooses its own. This guard is what keeps it on `/change-password`:
 * put it on every signed-in route *except* that one, and the only way out is to change the password.
 *
 * It answers `true` for a signed-out caller rather than redirecting, because `authGuard` runs
 * alongside it and owns that decision.
 */
export const passwordChangeGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  return authService.ensureLoaded().pipe(
    map((status) => {
      if (!status.authenticated || !status.mustChangePassword) {
        return true;
      }

      return router.createUrlTree(['/change-password']);
    }),
  );
};
