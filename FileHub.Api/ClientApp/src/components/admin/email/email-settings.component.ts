import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  IEmailSettings,
  emailIsConfigured,
  passwordPlaceholder,
  secureSocketOptions,
} from '@models/IEmailSettings';
import { apiErrorMessage } from '@services/api-error';
import { AuthService } from '@services/auth.service';
import { EmailSettingsService } from '@services/email-settings.service';
import { ToastrService } from 'ngx-toastr';
import { finalize } from 'rxjs';

/**
 * The SMTP settings. Everything about an account's life — the invitation, a password reset, an
 * address change — arrives by mail, so a broken host here is a FileHub nobody new can get into.
 *
 * Two rules of the API drive this form. The stored password is never readable, so an empty box
 * means "keep it" rather than "clear it" and has to say so. And the only way to know the settings
 * work is to send with them, which is what the test block is for: it reports the SMTP error it
 * gets back rather than "something went wrong".
 */
@Component({
  selector: 'admin-email-settings',
  standalone: true,
  imports: [FormsModule],
  templateUrl: 'email-settings.component.html',
  styleUrl: 'email-settings.component.scss',
})
export class EmailSettingsComponent implements OnInit {
  private readonly emailService = inject(EmailSettingsService);
  private readonly authService = inject(AuthService);
  private readonly toastr = inject(ToastrService);

  public readonly options = secureSocketOptions;

  public readonly smtpHost = signal('');
  public readonly port = signal(587);
  public readonly username = signal('');
  public readonly password = signal('');
  public readonly fromAddress = signal('');
  public readonly fromName = signal('');
  public readonly security = signal('Auto');
  public readonly hasPassword = signal(false);

  public readonly saving = signal(false);
  public readonly sending = signal(false);

  /** Prefilled with the signed-in admin's own address — the usual place to send a test. */
  public readonly recipient = signal(this.authService.status()?.email ?? '');

  /** The last test's outcome, kept on screen: this is the answer the admin came here for. */
  public readonly testError = signal('');
  public readonly testSent = signal(false);

  public readonly configured = computed(() => this.smtpHost().trim().length > 0);
  public readonly passwordHint = computed(() => passwordPlaceholder(this.hasPassword()));

  public ngOnInit(): void {
    this.emailService.get().subscribe({
      next: (settings) => this.apply(settings),
      error: (error: unknown) =>
        this.toastr.error(apiErrorMessage(error, 'Could not load the email settings')),
    });
  }

  public save(): void {
    this.saving.set(true);
    this.emailService
      .update({
        smtpHost: this.smtpHost().trim(),
        port: this.port(),
        username: this.username().trim(),
        // Empty means "keep the stored one" — the API is explicit about it, so nothing is cleared
        // by leaving the box alone.
        password: this.password(),
        fromAddress: this.fromAddress().trim(),
        fromName: this.fromName().trim(),
        secureSocketOptions: this.security(),
      })
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: (settings) => {
          this.apply(settings);
          this.toastr.success('Email settings saved');
        },
        error: (error: unknown) =>
          this.toastr.error(apiErrorMessage(error, 'Could not save the email settings')),
      });
  }

  public sendTest(): void {
    this.sending.set(true);
    this.testError.set('');
    this.testSent.set(false);

    this.emailService
      .sendTest(this.recipient().trim())
      .pipe(finalize(() => this.sending.set(false)))
      .subscribe({
        next: () => {
          this.testSent.set(true);
          this.toastr.success(`Test email sent to ${this.recipient().trim()}`);
        },
        error: (error: unknown) => {
          // A bad host comes back as a 502 carrying the SMTP failure. That text is the diagnosis,
          // so it stays on the screen rather than in a toast that scrolls away.
          this.testError.set(apiErrorMessage(error, 'The test email could not be sent'));
          this.toastr.error('The test email could not be sent');
        },
      });
  }

  private apply(settings: IEmailSettings): void {
    this.smtpHost.set(settings.smtpHost);
    this.port.set(settings.port);
    this.username.set(settings.username);
    this.fromAddress.set(settings.fromAddress);
    this.fromName.set(settings.fromName);
    this.security.set(settings.secureSocketOptions || 'Auto');
    this.hasPassword.set(settings.hasPassword);
    // Never round-trip what was typed: the box is a replacement, and leaving it filled would
    // resend the same secret on the next save for no reason.
    this.password.set('');

    if (!emailIsConfigured(settings)) {
      this.testSent.set(false);
    }
  }
}
