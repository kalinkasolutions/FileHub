import { Component } from '@angular/core';
import { ChangeEmailComponent } from '@components/Account/ChangeEmail/change-email.component';
import { AccountChangePasswordComponent } from '@components/Account/ChangePassword/change-password.component';
import { ProfileComponent } from '@components/Account/Profile/profile.component';
import { SessionsComponent } from '@components/Account/Sessions/sessions.component';
import { TwoFactorComponent } from '@components/Account/TwoFactor/two-factor.component';

/**
 * The account screen. It only puts the blocks in order and supplies the scroll area around them:
 * each one is a self-contained standalone component that loads its own state through
 * `AccountService`, so they can be rearranged here or reused on their own elsewhere.
 * `AccountService.ensureLoaded` shares one profile request across however many are on screen.
 *
 * There is deliberately no delete-account block — in FileHub an administrator removes an account.
 */
@Component({
  selector: 'account',
  standalone: true,
  imports: [
    ProfileComponent,
    ChangeEmailComponent,
    AccountChangePasswordComponent,
    TwoFactorComponent,
    SessionsComponent,
  ],
  templateUrl: 'account.component.html',
  styleUrl: 'account.component.scss',
})
export class AccountComponent {}
