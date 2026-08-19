import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ILoginResult } from '@models/ILogin';
import { apiErrorMessage } from '@services/api-error';
import { AuthService } from '@services/auth.service';
import { ToastrService } from 'ngx-toastr';
import { finalize } from 'rxjs';

/** 'credentials' is email + password; 'twoFactor' is the second step for accounts that have 2FA on. */
type LoginStep = 'credentials' | 'twoFactor';

@Component({
  selector: 'login',
  standalone: true,
  imports: [FormsModule, RouterLink],
  templateUrl: 'login.component.html',
  styleUrl: 'login.component.scss',
})
export class LoginComponent {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly toastr = inject(ToastrService);

  public readonly email = signal('');
  public readonly password = signal('');
  public readonly code = signal('');
  public readonly rememberMachine = signal(false);

  public readonly step = signal<LoginStep>('credentials');
  public readonly isLoading = signal(false);

  public onSubmit(): void {
    this.isLoading.set(true);
    this.authService
      .login({ email: this.email(), password: this.password() })
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: (result) => {
          if (result.requiresTwoFactor) {
            // The password was accepted but no session exists yet; the code finishes the sign-in.
            this.password.set('');
            this.step.set('twoFactor');
            return;
          }
          this.signedIn(result);
        },
        error: (error: unknown) => this.toastr.error(apiErrorMessage(error, 'Could not sign in')),
      });
  }

  public onSubmitCode(): void {
    this.isLoading.set(true);
    this.authService
      .loginTwoFactor({ code: this.code(), rememberMachine: this.rememberMachine() })
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: (result) => this.signedIn(result),
        error: (error: unknown) => {
          this.code.set('');
          this.toastr.error(apiErrorMessage(error, 'Could not verify that code'));
        },
      });
  }

  /** Back to step one, e.g. to sign in as someone else. */
  public restart(): void {
    this.code.set('');
    this.password.set('');
    this.step.set('credentials');
  }

  /**
   * An account that was invited, or whose password an admin reset, is signed in but may only go to
   * the password screen — the guard would send it there anyway, so go there directly.
   */
  private signedIn(result: ILoginResult): void {
    this.toastr.success('Signed in');
    this.router.navigate([result.mustChangePassword ? '/change-password' : '/']);
  }
}
