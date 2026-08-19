import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { apiErrorMessage } from '@services/api-error';
import { AuthService } from '@services/auth.service';
import { ToastrService } from 'ngx-toastr';
import { finalize } from 'rxjs';

/**
 * Landing page for the link in an invitation mail. There is no registration in FileHub — this is
 * the only way an account gets its first password, and redeeming the token also confirms the
 * address. Anonymous: the link is opened from a mail app, where no session exists.
 */
@Component({
  selector: 'accept-invite',
  standalone: true,
  imports: [FormsModule, RouterLink],
  templateUrl: 'accept-invite.component.html',
  styleUrl: 'accept-invite.component.scss',
})
export class AcceptInviteComponent {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly toastr = inject(ToastrService);

  private readonly userId = this.route.snapshot.queryParamMap.get('userId') ?? '';
  private readonly token = this.route.snapshot.queryParamMap.get('token') ?? '';

  /** Shown, not sent: the address is whatever the invitation was issued for. */
  public readonly email = signal(this.route.snapshot.queryParamMap.get('email') ?? '');
  public readonly hasValidLink = signal(!!this.userId && !!this.token);

  public readonly username = signal('');
  public readonly password = signal('');
  public readonly confirmPassword = signal('');
  public readonly isLoading = signal(false);

  /** Shown once the repeat has been typed into, rather than while it is still being typed. */
  public readonly mismatch = computed(
    () => this.confirmPassword().length > 0 && this.password() !== this.confirmPassword(),
  );

  public onSubmit(): void {
    if (this.password() !== this.confirmPassword()) {
      this.toastr.error('Passwords do not match');
      return;
    }

    this.isLoading.set(true);
    this.authService
      .acceptInvite({
        userId: this.userId,
        token: this.token,
        password: this.password(),
        username: this.username() || undefined,
      })
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: () => {
          this.toastr.success('Your account is ready. Sign in with your new password.');
          this.router.navigate(['/login']);
        },
        error: (error: unknown) =>
          this.toastr.error(apiErrorMessage(error, 'Could not accept this invitation')),
      });
  }
}
