import { Routes } from '@angular/router';
import { AdminComponent } from '@components/admin/admin.component';
import { FilebrowserComponent } from '@components/filebrowser/filebrowser.component';
import { LoginComponent } from '@components/Login/login.component';
import { NotFoundComponent } from '@components/notfound/notfound.component';
import { PublicShareComponent } from '@components/publicshare/publicshare.component';
import { adminGuard } from '@guards/admin.guard';
import { authGuard } from '@guards/auth.guard';
import { passwordChangeGuard } from '@guards/password-change.guard';

/**
 * Sign-in and the browser are eager — one of the two is the first thing every visit needs. The
 * screens reached from a link in an email are visited once in a while at most, so they are
 * `loadComponent` routes and stay out of the bundle that has to arrive before anything shows.
 *
 * Two guards sit on every signed-in route: `authGuard` for the session, `passwordChangeGuard` for
 * an account that still has to choose its own password. `share/:id` deliberately has neither — a
 * share link is public, and nginx serves it from the unauthenticated half.
 *
 * `data.chrome` is what each route asks of the app shell (`app.component.ts`): `none` for the
 * screens that carry their own FileHub mark and centre their own form, `anonymous` for the share
 * landing, which must look the same to a signed-in visitor as to the stranger the link was sent to.
 * Everything else gets the full header.
 */
export const routes: Routes = [
  { path: 'login', component: LoginComponent, data: { chrome: 'none' } },
  {
    path: 'accept-invite',
    data: { chrome: 'none' },
    loadComponent: () =>
      import('@components/AcceptInvite/accept-invite.component').then(
        (m) => m.AcceptInviteComponent,
      ),
  },
  {
    path: 'forgot-password',
    data: { chrome: 'none' },
    loadComponent: () =>
      import('@components/ForgotPassword/forgot-password.component').then(
        (m) => m.ForgotPasswordComponent,
      ),
  },
  {
    path: 'reset-password',
    data: { chrome: 'none' },
    loadComponent: () =>
      import('@components/ResetPassword/reset-password.component').then(
        (m) => m.ResetPasswordComponent,
      ),
  },
  {
    path: 'confirm-email-change',
    data: { chrome: 'none' },
    loadComponent: () =>
      import('@components/ConfirmEmailChange/confirm-email-change.component').then(
        (m) => m.ConfirmEmailChangeComponent,
      ),
  },
  {
    // No `passwordChangeGuard` here on purpose: this is the one screen a forced change may reach.
    // No chrome either — its way out is changing the password, not the header.
    path: 'change-password',
    canActivate: [authGuard],
    data: { chrome: 'none' },
    loadComponent: () =>
      import('@components/ChangePassword/change-password.component').then(
        (m) => m.ChangePasswordComponent,
      ),
  },
  {
    path: 'account',
    canActivate: [authGuard, passwordChangeGuard],
    loadComponent: () =>
      import('@components/Account/account.component').then((m) => m.AccountComponent),
  },
  {
    path: 'admin',
    component: AdminComponent,
    canActivate: [authGuard, passwordChangeGuard, adminGuard],
  },
  { path: 'share/:id', component: PublicShareComponent, data: { chrome: 'anonymous' } },
  // A real destination, not a fallback: an expired or used-up share link redirects a browser here.
  { path: '404', component: NotFoundComponent },
  {
    path: '',
    pathMatch: 'full',
    component: FilebrowserComponent,
    canActivate: [authGuard, passwordChangeGuard],
    data: { showPathSegments: true },
  },
  { path: '**', redirectTo: '404' },
];
