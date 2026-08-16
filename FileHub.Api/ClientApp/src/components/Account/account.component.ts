import { Component, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '@services/auth.service';
import { ToastrService } from 'ngx-toastr';

/**
 * Placeholder for the account screen. It carries only what the auth work needs — who is signed in,
 * the way to the password form and the way out — so the route exists and the header has somewhere
 * to point. The real screen is a stack of self-contained blocks (profile, email, two-factor, …),
 * each loading its own state through `AccountService`; add them here.
 */
@Component({
  selector: 'account',
  standalone: true,
  imports: [RouterLink],
  templateUrl: 'account.component.html',
  styleUrl: 'account.component.scss',
})
export class AccountComponent {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly toastr = inject(ToastrService);

  public readonly status = this.authService.status;
  public readonly isAdmin = this.authService.isAdmin;

  public logout(): void {
    this.authService.logout().subscribe({
      next: () => {
        this.toastr.success('Signed out');
        this.router.navigate(['/login']);
      },
      // A failed sign-out still means the user wants out; the interceptor handles a dead cookie.
      error: () => this.router.navigate(['/login']),
    });
  }
}
