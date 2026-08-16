import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AccountService } from '@services/account.service';
import { apiErrorMessage } from '@services/api-error';
import { ToastrService } from 'ngx-toastr';
import { firstValueFrom } from 'rxjs';

/**
 * Current password, then the new one twice — the API's DTO wants all three and compares the last
 * two server-side, so the repeat is checked here as well to fail before the round trip.
 * Self-contained, so it can be shown on a screen of its own too.
 */
@Component({
  selector: 'account-change-password',
  standalone: true,
  imports: [FormsModule],
  templateUrl: 'change-password.component.html',
  styleUrl: 'change-password.component.scss',
})
export class AccountChangePasswordComponent {
  /** Mirrors the DTO's `[MinLength(8)]` on `NewPassword`. */
  public static readonly MinimumLength = 8;

  private readonly accountService = inject(AccountService);
  private readonly toastr = inject(ToastrService);

  public readonly currentPassword = signal('');
  public readonly newPassword = signal('');
  public readonly confirmPassword = signal('');
  public readonly busy = signal(false);

  /** Shown once the repeat has been typed into, rather than while it is still being typed. */
  public readonly mismatch = computed(
    () => this.confirmPassword().length > 0 && this.newPassword() !== this.confirmPassword(),
  );

  /** Also shown only once typing has started, so the field doesn't open with an error under it. */
  public readonly tooShort = computed(
    () =>
      this.newPassword().length > 0 &&
      this.newPassword().length < AccountChangePasswordComponent.MinimumLength,
  );

  public readonly canSubmit = computed(
    () =>
      !this.busy() &&
      this.currentPassword().length > 0 &&
      this.newPassword().length >= AccountChangePasswordComponent.MinimumLength &&
      this.newPassword() === this.confirmPassword(),
  );

  public async submit(): Promise<void> {
    if (!this.canSubmit()) {
      return;
    }

    this.busy.set(true);
    try {
      await firstValueFrom(
        this.accountService.changePassword({
          currentPassword: this.currentPassword(),
          newPassword: this.newPassword(),
          confirmPassword: this.confirmPassword(),
        }),
      );
      this.reset();
      this.toastr.success('Password changed. Your other devices have been signed out.');
    } catch (error: unknown) {
      this.toastr.error(apiErrorMessage(error, 'Could not change your password'));
    } finally {
      this.busy.set(false);
    }
  }

  private reset(): void {
    this.currentPassword.set('');
    this.newPassword.set('');
    this.confirmPassword.set('');
  }
}
