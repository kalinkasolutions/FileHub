import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { IChangePassword } from '@models/IPassword';
import { Observable, firstValueFrom, switchMap, tap } from 'rxjs';
import { AuthService } from './auth.service';

/** Shape of `GET /api/account` — everything the account screen shows about the signed-in user. */
export interface IAccount {
  userId: string;
  /** Display name shown next to the shares this user created; not what the account signs in with. */
  username: string;
  /** The address used to sign in, to invite this account and to reset its password. */
  email: string;
  emailConfirmed: boolean;
  twoFactorEnabled: boolean;
  /** Unused recovery codes left; 0 while two-factor is off. */
  recoveryCodesLeft: number;
  mustChangePassword: boolean;
  memberSince: string;
}

/** `POST /api/account/email`: the address moves only once the link mailed to it is followed. */
export interface IChangeEmail {
  email: string;
  currentPassword: string;
}

/** `GET /api/account/2fa/setup` — a pending authenticator secret, not yet the account's factor. */
export interface ITwoFactorSetup {
  /** The secret in groups of four, for typing into an app by hand. */
  sharedKey: string;
  /** The `otpauth://` URI to render as a QR code. */
  authenticatorUri: string;
}

/** Shown exactly once: only their hashes are kept. */
export interface IRecoveryCodes {
  codes: string[];
}

/**
 * The signed-in user's own account. The profile lives in a shared signal so the account blocks can
 * be dropped anywhere — several on one screen, or one on its own — and still show the same state:
 * whichever renders first fills the signal via {@link ensureLoaded}, the rest read it, and every
 * mutation that changes the profile writes it back.
 */
@Injectable({ providedIn: 'root' })
export class AccountService {
  private readonly http = inject(HttpClient);
  private readonly authService = inject(AuthService);

  /** null until the first load resolves; cleared by {@link clear} on sign-out. */
  public readonly account = signal<IAccount | null>(null);

  /** The load while it is in flight, so N blocks on one screen make one request. */
  private loading?: Promise<void>;

  /** Loads the profile once. Concurrent callers share the in-flight request. */
  public ensureLoaded(): Promise<void> {
    if (this.account()) {
      return Promise.resolve();
    }

    this.loading ??= this.refresh().finally(() => (this.loading = undefined));
    return this.loading;
  }

  public async refresh(): Promise<void> {
    this.account.set(await firstValueFrom(this.http.get<IAccount>('/api/account')));
  }

  /** Drops the cached profile — the account screen of the *next* session must not show this one. */
  public clear(): void {
    this.account.set(null);
    this.loading = undefined;
  }

  /** The username is a display name; the email address is the identifier. */
  public async updateUsername(username: string): Promise<void> {
    this.account.set(
      await firstValueFrom(this.http.put<IAccount>('/api/account/username', { username })),
    );
  }

  /**
   * Changing the password is also what clears a forced change, so the cached status is re-read
   * afterwards — otherwise the guard would keep the user on `/change-password` they just left. The
   * profile goes with it, since it carries `mustChangePassword` too.
   *
   * `ConfirmPassword` is required by the DTO and compared server-side. A caller that has already
   * matched the two fields itself may leave it out rather than pass the same string twice.
   */
  public changePassword(data: IChangePassword): Observable<unknown> {
    return this.http.post<void>('/api/account/password', data).pipe(
      switchMap(() => this.authService.refresh()),
      // Only if a block is on screen holding it: the forced-change screen never loads the profile,
      // and re-reading it there would be a request nothing renders.
      tap(() => this.account() && void this.refresh()),
    );
  }

  /**
   * Starts an email change. Nothing on the account moves yet: the API mails a confirmation link to
   * the *new* address, and only opening that link completes the change — so a failure to send is
   * an error, not a warning.
   */
  public changeEmail(data: IChangeEmail): Promise<unknown> {
    return firstValueFrom(this.http.post('/api/account/email', data));
  }

  /**
   * Ends every other session. The server revalidates the security stamp on an interval, so other
   * devices keep working for up to a minute; this one is refreshed by the API itself and stays in.
   */
  public signOutEverywhere(): Promise<unknown> {
    return firstValueFrom(this.http.post('/api/account/sign-out-everywhere', {}));
  }

  /**
   * Hands out a fresh authenticator secret. Two-factor stays off until a code verifies.
   *
   * All four two-factor calls send the account password, not just the one that turns it off: each
   * changes what it takes to sign in as this account, and a session cookie is a weaker thing to hold
   * than the password. That is also why this one is a POST rather than a GET.
   */
  public twoFactorSetup(currentPassword: string): Promise<ITwoFactorSetup> {
    return firstValueFrom(
      this.http.post<ITwoFactorSetup>('/api/account/2fa/setup', { currentPassword }),
    );
  }

  public async enableTwoFactor(code: string, currentPassword: string): Promise<string[]> {
    const result = await firstValueFrom(
      this.http.post<IRecoveryCodes>('/api/account/2fa/enable', { code, currentPassword }),
    );
    await this.refresh();
    return result.codes;
  }

  public async disableTwoFactor(currentPassword: string): Promise<void> {
    await firstValueFrom(this.http.post('/api/account/2fa/disable', { currentPassword }));
    await this.refresh();
  }

  /** Replaces the whole set: whatever was written down before this call stops working. */
  public async regenerateRecoveryCodes(currentPassword: string): Promise<string[]> {
    const result = await firstValueFrom(
      this.http.post<IRecoveryCodes>('/api/account/2fa/recovery-codes', { currentPassword }),
    );
    await this.refresh();
    return result.codes;
  }
}
