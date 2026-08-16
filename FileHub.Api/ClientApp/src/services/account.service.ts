import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { IChangePassword } from '@models/IPassword';
import { Observable, switchMap } from 'rxjs';
import { AuthService } from './auth.service';

/**
 * The signed-in user's own account. Only the password lives here so far, because the forced
 * password change needs it — the rest of the account screen (profile, email, two-factor, …)
 * belongs to whoever builds it and should be added to this service.
 */
@Injectable({ providedIn: 'root' })
export class AccountService {
  private readonly http = inject(HttpClient);
  private readonly authService = inject(AuthService);

  /**
   * Changing the password is also what clears a forced change, so the cached status is re-read
   * afterwards — otherwise the guard would keep the user on `/change-password` they just left.
   */
  public changePassword(data: IChangePassword): Observable<unknown> {
    return this.http
      .post<void>('/api/account/password', data)
      .pipe(switchMap(() => this.authService.refresh()));
  }
}
