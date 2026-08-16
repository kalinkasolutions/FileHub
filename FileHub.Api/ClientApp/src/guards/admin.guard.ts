import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { Roles } from '@models/roles';
import { AuthService } from '@services/auth.service';
import { map } from 'rxjs';

/**
 * The admin area. A signed-out caller is sent to sign in; a signed-in one without the role is sent
 * back to the browser rather than to the sign-in screen, which would only look like a bug to them.
 * This is a convenience, not the control: the API checks the role on every admin call itself.
 */
export const adminGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  return authService.ensureLoaded().pipe(
    map((status) => {
      if (!status.authenticated) {
        return router.createUrlTree(['/login']);
      }

      return status.roles.includes(Roles.Admin) ? true : router.createUrlTree(['/']);
    }),
  );
};
