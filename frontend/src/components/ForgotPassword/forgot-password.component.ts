import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { apiErrorMessage } from '@services/api-error';
import { AuthService } from '@services/auth.service';
import { ToastrService } from 'ngx-toastr';
import { finalize } from 'rxjs';

@Component({
  selector: 'forgot-password',
  standalone: true,
  imports: [FormsModule, RouterLink],
  templateUrl: 'forgot-password.component.html',
  styleUrl: 'forgot-password.component.scss',
})
export class ForgotPasswordComponent {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly toastr = inject(ToastrService);

  public readonly email = signal('');
  public readonly isLoading = signal(false);

  public onSubmit(): void {
    this.isLoading.set(true);
    this.authService
      .forgotPassword({ email: this.email() })
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: () => {
          // The endpoint reports success whether or not the account exists, and so does this.
          this.toastr.success('If an account exists for that email, a reset link is on its way.');
          this.router.navigate(['/login']);
        },
        error: (error: unknown) =>
          this.toastr.error(apiErrorMessage(error, 'Could not send the reset link')),
      });
  }
}
