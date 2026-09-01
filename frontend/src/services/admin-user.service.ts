import { HttpClient } from '@angular/common/http';
import { Injectable, Signal, inject, signal } from '@angular/core';
import { IAdminUser, IInviteResult, IInviteUser, IUpdateUser, sortUsers } from '@models/IAdminUser';
import { Observable, finalize, switchMap, tap } from 'rxjs';

const url = '/api/admin/users';

/**
 * Account administration. This is the only way an account comes into existence — FileHub has no
 * registration page — so every route here is admin-only and the API checks the role itself.
 *
 * The list is a signal because two screens read it: the user rows and the "who may see this base
 * path" editor, which needs names for the ids it ticks. Mutations that can change more than the
 * one row they touch (roles, lockout) re-read the whole list instead of patching it, which also
 * keeps the role counts on the same screen honest.
 */
@Injectable({ providedIn: 'root' })
export class AdminUserService {
  private readonly http = inject(HttpClient);

  private readonly settled = signal(false);
  private readonly state = signal<IAdminUser[]>([]);

  public readonly users: Signal<IAdminUser[]> = this.state.asReadonly();

  /**
   * True once the first read settled, however it settled. Every message that says there are no
   * accounts — a section's own empty line, and a grant editor's "there is nothing to tick" — is gated
   * on it, because the loading overlay is raised for writes only: ungated, such a message claims
   * the installation has none for as long as the request takes.
   *
   * It belongs here rather than on each screen for the same reason the list does. Several sections
   * read this one collection, and a flag per screen would drop back to false every time a tab was
   * reopened onto rows that are already in hand.
   */
  public readonly loaded: Signal<boolean> = this.settled.asReadonly();

  public load(): Observable<IAdminUser[]> {
    return this.http.get<IAdminUser[]>(url).pipe(
      tap((users) => this.state.set(sortUsers(users))),
      finalize(() => this.settled.set(true)),
    );
  }

  /**
   * Creates the account and mails the invitation. A false `inviteMailSent` still means the account
   * exists, so the caller has to say so rather than report a failure.
   */
  public invite(user: IInviteUser): Observable<IInviteResult> {
    return this.http.post<IInviteResult>(url, user);
  }

  public resendInvite(id: string): Observable<void> {
    return this.http.post<void>(`${url}/${id}/resend-invite`, {});
  }

  /** The email address has to be the one the account already has; the API refuses a change here. */
  public update(id: string, user: IUpdateUser): Observable<IAdminUser[]> {
    return this.http.put<void>(`${url}/${id}`, user).pipe(switchMap(() => this.load()));
  }

  /** Disables (`true`) or re-enables (`false`) an account. Refused for the last usable admin. */
  public setLockout(id: string, locked: boolean): Observable<IAdminUser[]> {
    return this.http
      .put<void>(`${url}/${id}/lockout`, { locked })
      .pipe(switchMap(() => this.load()));
  }

  public remove(id: string): Observable<IAdminUser[]> {
    return this.http.delete<void>(`${url}/${id}`).pipe(switchMap(() => this.load()));
  }
}
