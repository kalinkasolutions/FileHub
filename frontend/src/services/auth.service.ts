import { HttpClient } from '@angular/common/http';
import { Injectable, Signal, computed, inject, signal } from '@angular/core';
import { IAcceptInvite, IConfirmEmailChange } from '@models/IInvite';
import { IAuthStatus } from '@models/IAuthStatus';
import { ILogin, ILoginResult, ITwoFactorLogin } from '@models/ILogin';
import { IForgotPassword, IResetPassword } from '@models/IPassword';
import { Roles } from '@models/roles';
import {
  Observable,
  catchError,
  finalize,
  of,
  retry,
  shareReplay,
  tap,
  timeout,
  timer,
} from 'rxjs';

/** What the app knows about a caller with no session. */
const anonymous: IAuthStatus = {
  authenticated: false,
  userId: null,
  username: null,
  email: null,
  roles: [],
  mustChangePassword: false,
};

/**
 * A busy connection — a multi-GB download sharing it, say — can stall the status request for tens
 * of seconds. Each attempt gets this long before it counts as failed and is tried again.
 */
const statusAttemptTimeoutMs = 5_000;
const statusRetries = 3;
const firstRetryDelayMs = 500;

/**
 * The session and everything that depends on it. The session itself is a cookie, so this service
 * never holds a token — it holds the *answer* to `GET /api/auth/status`, which is what the guards
 * and the header read.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);

  /** null = not resolved yet. Resolved once by {@link ensureLoaded}, cleared by {@link invalidate}. */
  private readonly state = signal<IAuthStatus | null>(null);

  /**
   * The status request while it is in flight. Several guards can run for one navigation and each
   * calls `ensureLoaded`; sharing the observable means they all wait on the same request.
   */
  private inFlight: Observable<IAuthStatus | null> | null = null;

  public readonly status: Signal<IAuthStatus | null> = this.state.asReadonly();
  public readonly isAuthenticated = computed(() => this.state()?.authenticated ?? false);
  public readonly isAdmin = computed(() => this.state()?.roles.includes(Roles.Admin) ?? false);

  /**
   * Whether to offer the share controls at all. The API refuses without the role either way — this
   * is what keeps a button on screen from being one that always fails.
   */
  public readonly canCreateShares = computed(
    () => this.state()?.roles.includes(Roles.CreateShares) ?? false,
  );
  public readonly mustChangePassword = computed(() => this.state()?.mustChangePassword ?? false);
  public readonly username = computed(() => this.state()?.username ?? '');

  /**
   * The cached status, fetching it first if nothing has yet.
   *
   * Emits null when the server could not be asked. The endpoint answers anonymous callers too, so a
   * failure is the network or a stopped server and says nothing about the session: it is neither
   * cached nor taken as signed out. A session that really is gone shows up as a 401 on the page's
   * own calls, which `unauthorizedInterceptor` turns into the sign-in screen.
   */
  public ensureLoaded(): Observable<IAuthStatus | null> {
    const cached = this.state();
    if (cached) {
      return of(cached);
    }

    if (this.inFlight) {
      return this.inFlight;
    }

    this.inFlight = this.http.get<IAuthStatus>('/api/auth/status').pipe(
      timeout(statusAttemptTimeoutMs),
      retry({
        count: statusRetries,
        delay: (_, attempt) => timer(firstRetryDelayMs * 2 ** (attempt - 1)),
      }),
      tap((status) => this.state.set(status)),
      catchError(() => of(null)),
      finalize(() => (this.inFlight = null)),
      shareReplay({ bufferSize: 1, refCount: false }),
    );

    return this.inFlight;
  }

  /** Drops the cached status. The next {@link ensureLoaded} asks the server again. */
  public invalidate(): void {
    this.state.set(null);
    this.inFlight = null;
  }

  /** Re-reads the status now — after a password change, say, which clears `mustChangePassword`. */
  public refresh(): Observable<IAuthStatus | null> {
    this.invalidate();
    return this.ensureLoaded();
  }

  /**
   * Checks the password. A `requiresTwoFactor` result means no session exists yet and
   * {@link loginTwoFactor} has to finish the sign-in, so nothing is cached for it.
   */
  public login(data: ILogin): Observable<ILoginResult> {
    return this.http.post<ILoginResult>('/api/auth/login', data).pipe(
      tap((result) => {
        if (!result.requiresTwoFactor) {
          this.invalidate();
        }
      }),
    );
  }

  /** Second step of a two-factor sign-in: an authenticator code or a recovery code. */
  public loginTwoFactor(data: ITwoFactorLogin): Observable<ILoginResult> {
    return this.http
      .post<ILoginResult>('/api/auth/login-2fa', data)
      .pipe(tap(() => this.invalidate()));
  }

  public logout(): Observable<void> {
    return this.http.post<void>('/api/auth/logout', {}).pipe(tap(() => this.state.set(anonymous)));
  }

  // ─── Anonymous flows ──────────────────────────────────────────────────────
  // All four are opened from a mail link, usually in a browser with no session at all.

  /** Sets the first password on an invited account and confirms its address. */
  public acceptInvite(data: IAcceptInvite): Observable<void> {
    return this.http.post<void>('/api/auth/accept-invite', data);
  }

  /** Always reports success, so it can't be used to probe for accounts. */
  public forgotPassword(data: IForgotPassword): Observable<void> {
    return this.http.post<void>('/api/auth/forgot-password', data);
  }

  public resetPassword(data: IResetPassword): Observable<void> {
    return this.http.post<void>('/api/auth/reset-password', data);
  }

  public confirmEmailChange(data: IConfirmEmailChange): Observable<void> {
    return this.http.post<void>('/api/auth/confirm-email-change', data);
  }
}
