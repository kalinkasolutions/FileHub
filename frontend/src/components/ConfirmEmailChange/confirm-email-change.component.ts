import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AuthService } from '@services/auth.service';

type ConfirmState = 'confirming' | 'success' | 'error';

/**
 * Landing page for the link mailed to a user's new address when they change their email. Anonymous:
 * the link is usually opened from a mail app, where the session may not exist.
 */
@Component({
  selector: 'confirm-email-change',
  standalone: true,
  imports: [RouterLink],
  templateUrl: 'confirm-email-change.component.html',
  styleUrl: 'confirm-email-change.component.scss',
})
export class ConfirmEmailChangeComponent implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly route = inject(ActivatedRoute);

  public readonly state = signal<ConfirmState>('confirming');
  public readonly email = signal('');

  public ngOnInit(): void {
    const userId = this.route.snapshot.queryParamMap.get('userId');
    const email = this.route.snapshot.queryParamMap.get('email');
    const token = this.route.snapshot.queryParamMap.get('token');

    if (!userId || !email || !token) {
      this.state.set('error');
      return;
    }

    this.email.set(email);
    this.authService.confirmEmailChange({ userId, email, token }).subscribe({
      next: () => this.state.set('success'),
      error: () => this.state.set('error'),
    });
  }
}
