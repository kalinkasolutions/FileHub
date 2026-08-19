import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { AccountService } from '@services/account.service';
import { apiErrorMessage } from '@services/api-error';
import { ToastrService } from 'ngx-toastr';

/**
 * Who you are: the display name (editable in place), the sign-in address with its confirmation
 * state, and how long the account has existed. Self-contained — it loads the profile itself, so it
 * can be dropped on any screen.
 */
@Component({
  selector: 'account-profile',
  standalone: true,
  imports: [FormsModule, MatIconModule, DatePipe],
  templateUrl: 'profile.component.html',
  styleUrl: 'profile.component.scss',
})
export class ProfileComponent implements OnInit {
  private readonly accountService = inject(AccountService);
  private readonly toastr = inject(ToastrService);

  public readonly account = this.accountService.account;
  public readonly editing = signal(false);
  public readonly username = signal('');
  public readonly busy = signal(false);

  public async ngOnInit(): Promise<void> {
    try {
      await this.accountService.ensureLoaded();
    } catch (error: unknown) {
      this.toastr.error(apiErrorMessage(error, 'Could not load your account'));
    }
  }

  public startEdit(): void {
    this.username.set(this.account()?.username ?? '');
    this.editing.set(true);
  }

  public cancelEdit(): void {
    this.editing.set(false);
  }

  public async saveUsername(): Promise<void> {
    const username = this.username().trim();
    if (!username || username === this.account()?.username) {
      this.editing.set(false);
      return;
    }

    this.busy.set(true);
    try {
      await this.accountService.updateUsername(username);
      this.editing.set(false);
      this.toastr.success('Display name updated');
    } catch (error: unknown) {
      this.toastr.error(apiErrorMessage(error, 'Could not change your display name'));
    } finally {
      this.busy.set(false);
    }
  }
}
