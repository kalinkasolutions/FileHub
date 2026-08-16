import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { apiErrorMessage } from '@services/api-error';
import { AuthService } from '@services/auth.service';
import { ToastrService } from 'ngx-toastr';
import { finalize } from 'rxjs';

/** Landing page for the link in a password-reset mail. Anonymous, like every mail-link screen. */
@Component({
  selector: 'reset-password',
  standalone: true,
  imports: [FormsModule, RouterLink],
  templateUrl: 'reset-password.component.html',
  styleUrl: 'reset-password.component.scss',
})
export class ResetPasswordComponent {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly toastr = inject(ToastrService);

  private readonly email = this.route.snapshot.queryParamMap.get('email') ?? '';
  private readonly token = this.route.snapshot.queryParamMap.get('token') ?? '';
  public readonly hasValidLink = signal(!!this.email && !!this.token);

  public readonly password = signal('');
  public readonly confirmPassword = signal('');
  public readonly isLoading = signal(false);

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
      .resetPassword({ email: this.email, token: this.token, password: this.password() })
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: () => {
          this.toastr.success('Password reset. You can now sign in.');
          this.router.navigate(['/login']);
        },
        error: (error: unknown) =>
          this.toastr.error(apiErrorMessage(error, 'Could not reset your password')),
      });
  }
}
