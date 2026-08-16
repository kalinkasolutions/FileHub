import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { AccountService } from '@services/account.service';
import { apiErrorMessage } from '@services/api-error';
import { ToastrService } from 'ngx-toastr';

/** Which part of the flow is on screen; `codes` is the one-time reveal of the recovery codes. */
export type TwoFactorView = 'status' | 'setup' | 'codes';

/**
 * Authenticator-app (TOTP) two-factor setup and teardown. The secret is fetched on demand, shown as
 * a QR code and as a typeable key, and only becomes the account's second factor once a generated
 * code verifies — at which point the recovery codes are shown once and never again.
 */
@Component({
  selector: 'account-two-factor',
  standalone: true,
  imports: [FormsModule, MatIconModule],
  templateUrl: 'two-factor.component.html',
  styleUrl: 'two-factor.component.scss',
})
export class TwoFactorComponent implements OnInit {
  /** Below this, the "generate a new set" prompt turns into a warning. */
  public static readonly LowOnCodes = 3;

  private readonly accountService = inject(AccountService);
  private readonly toastr = inject(ToastrService);

  public readonly account = this.accountService.account;
  public readonly view = signal<TwoFactorView>('status');
  public readonly busy = signal(false);

  public readonly sharedKey = signal('');
  public readonly authenticatorUri = signal('');
  public readonly qrCode = signal('');
  public readonly code = signal('');

  public readonly recoveryCodes = signal<string[]>([]);

  /**
   * The account password, which every one of these operations asks for — pairing an authenticator
   * and minting recovery codes are credential changes just as much as turning the second factor off.
   * {@link setupPassword} is typed once and carried from "start setup" into the code verification, so
   * one flow is one prompt; each is cleared as soon as its flow ends.
   */
  public readonly setupPassword = signal('');
  public readonly regeneratePassword = signal('');
  public readonly disablePassword = signal('');

  /**
   * Running out of recovery codes with no authenticator to hand is how an account locks itself out,
   * so the count turns into a warning before it gets there. Only meaningful while 2FA is on.
   */
  public readonly lowOnCodes = computed(
    () => (this.account()?.recoveryCodesLeft ?? 0) <= TwoFactorComponent.LowOnCodes,
  );

  public async ngOnInit(): Promise<void> {
    try {
      await this.accountService.ensureLoaded();
    } catch (error: unknown) {
      this.toastr.error(apiErrorMessage(error, 'Could not load your account'));
    }
  }

  public async startSetup(): Promise<void> {
    if (!this.setupPassword()) {
      return;
    }

    this.busy.set(true);
    try {
      const setup = await this.accountService.twoFactorSetup(this.setupPassword());
      this.sharedKey.set(setup.sharedKey);
      this.authenticatorUri.set(setup.authenticatorUri);
      this.code.set('');
      this.qrCode.set('');
      this.view.set('setup');
    } catch (error: unknown) {
      this.toastr.error(apiErrorMessage(error, 'Could not start the setup'));
      this.busy.set(false);
      return;
    }

    // Drawing the QR comes after the screen is up, and on its own: the key and the app link are
    // enough to finish the setup, so a failing encoder must not take the whole screen down with it.
    try {
      this.qrCode.set(await this.renderQrCode(this.authenticatorUri()));
    } catch {
      this.qrCode.set('');
    } finally {
      this.busy.set(false);
    }
  }

  public cancelSetup(): void {
    this.setupPassword.set('');
    this.view.set('status');
  }

  public async verify(): Promise<void> {
    const code = this.code().trim();
    if (!code || !this.setupPassword()) {
      return;
    }

    this.busy.set(true);
    try {
      this.recoveryCodes.set(
        await this.accountService.enableTwoFactor(code, this.setupPassword()),
      );
      this.setupPassword.set('');
      this.view.set('codes');
      this.toastr.success('Two-factor authentication is on');
    } catch (error: unknown) {
      this.toastr.error(apiErrorMessage(error, 'Could not turn on two-factor authentication'));
    } finally {
      this.busy.set(false);
    }
  }

  public async regenerateCodes(): Promise<void> {
    if (!this.regeneratePassword()) {
      return;
    }

    this.busy.set(true);
    try {
      this.recoveryCodes.set(
        await this.accountService.regenerateRecoveryCodes(this.regeneratePassword()),
      );
      this.regeneratePassword.set('');
      this.view.set('codes');
      this.toastr.success('New recovery codes generated — the old ones no longer work');
    } catch (error: unknown) {
      this.toastr.error(apiErrorMessage(error, 'Could not generate new recovery codes'));
    } finally {
      this.busy.set(false);
    }
  }

  public async disable(): Promise<void> {
    if (!this.disablePassword()) {
      return;
    }

    this.busy.set(true);
    try {
      await this.accountService.disableTwoFactor(this.disablePassword());
      this.disablePassword.set('');
      this.view.set('status');
      this.toastr.success('Two-factor authentication is off');
    } catch (error: unknown) {
      this.toastr.error(apiErrorMessage(error, 'Could not turn off two-factor authentication'));
    } finally {
      this.busy.set(false);
    }
  }

  /** Done with the recovery codes: back to the status view, and they are gone for good. */
  public acknowledgeCodes(): void {
    this.recoveryCodes.set([]);
    this.view.set('status');
  }

  public copyCodes(): Promise<void> {
    return this.copyToClipboard(this.recoveryCodes().join('\n'), 'Recovery codes copied');
  }

  /** The key is what you type when a QR code can't be scanned, so it has to be easy to lift. */
  public copyKey(): Promise<void> {
    return this.copyToClipboard(this.sharedKey(), 'Key copied');
  }

  private async copyToClipboard(text: string, success: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(text);
      this.toastr.success(success);
    } catch {
      // Clipboard access is denied in some browsers and on any non-secure origin; both the key and
      // the codes are selectable on screen, so copying by hand is the fallback.
      this.toastr.error('Could not copy — select the text and copy it manually');
    }
  }

  /**
   * Loaded on demand: the QR encoder is only needed by the handful of users setting 2FA up, so it
   * stays out of the main bundle.
   *
   * `qrcode` is CommonJS, so the bundler hands its whole exports object over as the module's
   * `default`. Its type declarations describe named exports, which typecheck happily and then come
   * out `undefined` at runtime — hence going through the default here.
   */
  private async renderQrCode(uri: string): Promise<string> {
    const { default: qrcode } = await import('qrcode');
    return qrcode.toDataURL(uri, { margin: 1, width: 220 });
  }
}
