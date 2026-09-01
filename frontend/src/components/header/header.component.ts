import { Component, computed, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatIconModule } from '@angular/material/icon';
import { NavigationEnd, Router, RouterLink } from '@angular/router';
import { AccountService } from '@services/account.service';
import { AuthService } from '@services/auth.service';
import { ToastrService } from 'ngx-toastr';
import { filter } from 'rxjs';

/**
 * The app chrome: where you are, the way to the account screen, the way to the admin area when the
 * session has the role for it, and the way out.
 *
 * It renders for a caller with no session too — the auth screens and the public share page have
 * none — and in that state it is the brand and nothing else: no destinations a signed-out visitor
 * cannot reach, and no name to show. `anonymous` forces that state even for a caller who *is*
 * signed in, which is what the public share page is set to: that page belongs to whoever was sent
 * the link, and it must look the same to them as to anyone else.
 */
@Component({
  selector: 'app-header',
  standalone: true,
  imports: [MatIconModule, RouterLink],
  templateUrl: './header.component.html',
  styleUrl: './header.component.scss',
})
export class HeaderComponent {
  private readonly authService = inject(AuthService);
  private readonly accountService = inject(AccountService);
  private readonly router = inject(Router);
  private readonly toastr = inject(ToastrService);

  /** Set by the shell from the route's `data.chrome`; `true` pins the header to its signed-out form. */
  public readonly anonymous = input(false);

  /** `router.url` is a plain property, so it is mirrored into a signal the template can read. */
  private readonly url = signal(this.router.url);

  public readonly signedIn = computed(
    () => !this.anonymous() && this.authService.isAuthenticated(),
  );
  public readonly isAdmin = computed(() => this.signedIn() && this.authService.isAdmin());

  /** Which of the three destinations the current URL is in — the header's "where you are". */
  public readonly section = computed(() => {
    const url = this.url();
    if (url.startsWith('/admin')) {
      return 'Admin';
    }
    if (url.startsWith('/account')) {
      return 'Account';
    }
    if (url.startsWith('/about')) {
      return 'About';
    }
    return 'Files';
  });

  constructor() {
    this.router.events
      .pipe(
        filter((event) => event instanceof NavigationEnd),
        takeUntilDestroyed(),
      )
      .subscribe((event) => this.url.set(event.urlAfterRedirects));
  }

  public signOut(): void {
    this.authService.logout().subscribe({
      next: () => this.leave('Signed out'),
      // A failed sign-out still means the user wants out; the interceptor handles a dead cookie.
      error: () => this.leave(),
    });
  }

  private leave(message?: string): void {
    // The next session must not find this one's profile still in the shared signal.
    this.accountService.clear();
    if (message) {
      this.toastr.success(message);
    }
    void this.router.navigate(['/login']);
  }
}
