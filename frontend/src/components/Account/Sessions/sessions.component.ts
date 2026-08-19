import { Component, inject, signal } from '@angular/core';
import { AccountService } from '@services/account.service';
import { apiErrorMessage } from '@services/api-error';
import { ToastrService } from 'ngx-toastr';

/**
 * Signs the account out of every other browser and phone it is signed in on — for a device that was
 * lost, or a session left open somewhere. This device stays signed in: the API refreshes its cookie
 * on the way out.
 *
 * The confirm step is inline rather than a dialog so the block stays self-contained: it can be
 * dropped anywhere without dragging an overlay dependency along.
 */
@Component({
  selector: 'account-sessions',
  standalone: true,
  imports: [],
  templateUrl: 'sessions.component.html',
  styleUrl: 'sessions.component.scss',
})
export class SessionsComponent {
  private readonly accountService = inject(AccountService);
  private readonly toastr = inject(ToastrService);

  public readonly busy = signal(false);
  public readonly confirming = signal(false);

  public ask(): void {
    this.confirming.set(true);
  }

  public cancel(): void {
    this.confirming.set(false);
  }

  public async signOutEverywhere(): Promise<void> {
    this.busy.set(true);
    try {
      await this.accountService.signOutEverywhere();
      this.confirming.set(false);
      this.toastr.success('Your other devices will be signed out within a minute');
    } catch (error: unknown) {
      this.toastr.error(apiErrorMessage(error, 'Could not sign out your other devices'));
    } finally {
      this.busy.set(false);
    }
  }
}
