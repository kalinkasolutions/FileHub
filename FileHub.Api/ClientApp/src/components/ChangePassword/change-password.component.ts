import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AccountService } from '@services/account.service';
import { apiErrorMessage } from '@services/api-error';
import { AuthService } from '@services/auth.service';
import { ToastrService } from 'ngx-toastr';
import { finalize } from 'rxjs';

/**
 * The forced password change. An invited account, or one whose password an administrator reset,
 * carries `mustChangePassword`, and `passwordChangeGuard` bounces it here from everywhere else —
 * so this screen has to say *why* it is being shown, or it reads as the app being stuck.
 *
 * It is also reachable on purpose from the account screen, where the account is fine and only the
 * form is wanted; `isForced` is what tells the two apart.
 */
@Component({
  selector: 'change-password',
  standalone: true,
  imports: [FormsModule],
  templateUrl: 'change-password.component.html',
  styleUrl: 'change-password.component.scss',
})
export class ChangePasswordComponent {
  /** Mirrors the server's minimum. */
  private static readonly MinimumLength = 4;

  private readonly accountService = inject(AccountService);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly toastr = inject(ToastrService);

  public readonly isForced = this.authService.mustChangePassword;

  public readonly currentPassword = signal('');
  public readonly newPassword = signal('');
  public readonly confirmPassword = signal('');
  public readonly isLoading = signal(false);

  /** Shown once the repeat has been typed into, rather than while it is still being typed. */
  public readonly mismatch = computed(
    () => this.confirmPassword().length > 0 && this.newPassword() !== this.confirmPassword(),
  );

  public readonly canSubmit = computed(
    () =>
      !this.isLoading() &&
      this.currentPassword().length > 0 &&
      this.newPassword().length >= ChangePasswordComponent.MinimumLength &&
      this.newPassword() === this.confirmPassword(),
  );

  public onSubmit(): void {
    if (!this.canSubmit()) {
      return;
    }

    this.isLoading.set(true);
    this.accountService
      .changePassword({
        currentPassword: this.currentPassword(),
        newPassword: this.newPassword(),
      })
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: () => {
          this.reset();
          this.toastr.success('Password changed');
          // The service re-reads the status first, so `mustChangePassword` is already cleared and
          // the guard lets this navigation through.
          this.router.navigate(['/']);
        },
        error: (error: unknown) =>
          this.toastr.error(apiErrorMessage(error, 'Could not change your password')),
      });
  }

  private reset(): void {
    this.currentPassword.set('');
    this.newPassword.set('');
    this.confirmPassword.set('');
  }
}
