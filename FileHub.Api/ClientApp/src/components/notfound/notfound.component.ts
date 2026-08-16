import { Component, computed, inject } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { AuthService } from '@services/auth.service';

/**
 * A real destination, not a fallback screen: an expired or used-up share link redirects a browser
 * straight here, so most of the people who see it arrived from outside with no session. That is why
 * the way out is offered rather than assumed — signed in it is the browser, signed out it is the
 * sign-in screen, and a visitor who only ever had a link is told plainly that the link is dead.
 */
@Component({
  standalone: true,
  selector: 'not-found',
  templateUrl: './notfound.component.html',
  styleUrl: './notfound.component.scss',
  imports: [MatIconModule, RouterLink],
})
export class NotFoundComponent {
  private readonly authService = inject(AuthService);

  public readonly signedIn = this.authService.isAuthenticated;

  public readonly exit = computed(() => (this.signedIn() ? '/' : '/login'));
  public readonly exitLabel = computed(() => (this.signedIn() ? 'Back to your files' : 'Sign in'));
}
