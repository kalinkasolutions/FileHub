import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AccountService } from '@services/account.service';
import { apiErrorMessage } from '@services/api-error';
import { ToastrService } from 'ngx-toastr';

/**
 * Moves the account to another email address. That address is the sign-in identifier, so the change
 * needs the current password and lands only once the link mailed to the *new* address is opened.
 * Nothing on the account changes before that — which is why a mail that fails to send is reported
 * as an error rather than swallowed: without it there is no way to finish.
 */
@Component({
  selector: 'account-change-email',
  standalone: true,
  imports: [FormsModule],
  templateUrl: 'change-email.component.html',
  styleUrl: 'change-email.component.scss',
})
export class ChangeEmailComponent {
  private readonly accountService = inject(AccountService);
  private readonly toastr = inject(ToastrService);

  public readonly account = this.accountService.account;
  public readonly email = signal('');
  public readonly currentPassword = signal('');
  public readonly busy = signal(false);

  /** The address a link was sent to in this session, so the screen can say what it is waiting on. */
  public readonly pending = signal<string | null>(null);

  /** The last send failed — the change cannot complete, so it is stated on screen, not just toasted. */
  public readonly failed = signal<string | null>(null);

  public readonly canSubmit = computed(
    () => !this.busy() && this.email().includes('@') && this.currentPassword().length > 0,
  );

  public async submit(): Promise<void> {
    if (!this.canSubmit()) {
      return;
    }

    const email = this.email().trim();
    this.busy.set(true);
    try {
      await this.accountService.changeEmail({ email, currentPassword: this.currentPassword() });
      this.currentPassword.set('');
      this.email.set('');
      this.failed.set(null);
      this.pending.set(email);
      this.toastr.success(`Confirmation link sent to ${email}`);
    } catch (error: unknown) {
      const message = apiErrorMessage(error, 'Could not send the confirmation link');
      this.pending.set(null);
      this.failed.set(message);
      this.toastr.error(message);
    } finally {
      this.busy.set(false);
    }
  }
}
