import { HttpClient } from '@angular/common/http';
import { Injectable, Signal, inject, signal } from '@angular/core';
import { IAdminShare } from '@models/IAdminShare';
import { Observable, finalize, tap } from 'rxjs';

/**
 * Every public link in the installation, whoever made it. This is the admin's view of the share
 * table — the browsing side has its own service for the links the signed-in user created.
 */
@Injectable({ providedIn: 'root' })
export class AdminShareService {
  private readonly http = inject(HttpClient);

  private readonly settled = signal(false);
  private readonly state = signal<IAdminShare[]>([]);

  public readonly shares: Signal<IAdminShare[]> = this.state.asReadonly();

  /**
   * True once the first read settled, however it settled. Every message that says there are no
   * links — a section's own empty line, and a grant editor's "there is nothing to tick" — is gated
   * on it, because the loading overlay is raised for writes only: ungated, such a message claims
   * the installation has none for as long as the request takes.
   *
   * It belongs here rather than on each screen for the same reason the list does. Several sections
   * read this one collection, and a flag per screen would drop back to false every time a tab was
   * reopened onto rows that are already in hand.
   */
  public readonly loaded: Signal<boolean> = this.settled.asReadonly();

  public load(): Observable<IAdminShare[]> {
    return this.http.get<IAdminShare[]>('/api/admin/shares').pipe(
      tap((shares) => this.state.set(shares)),
      finalize(() => this.settled.set(true)),
    );
  }

  /** Revokes the link. The file stays where it is; only the public way to it is removed. */
  public remove(id: string): Observable<void> {
    return this.http
      .delete<void>(`/api/admin/share/${id}`)
      .pipe(tap(() => this.state.update((list) => list.filter((share) => share.id !== id))));
  }
}
