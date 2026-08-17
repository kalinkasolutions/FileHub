import { HttpClient } from '@angular/common/http';
import { Injectable, Signal, inject, signal } from '@angular/core';
import { IAdminShare } from '@models/IAdminShare';
import { Observable, tap } from 'rxjs';

/**
 * Every public link in the installation, whoever made it. This is the admin's view of the share
 * table — the browsing side has its own service for the links the signed-in user created.
 */
@Injectable({ providedIn: 'root' })
export class AdminShareService {
  private readonly http = inject(HttpClient);

  private readonly state = signal<IAdminShare[]>([]);

  public readonly shares: Signal<IAdminShare[]> = this.state.asReadonly();

  public load(): Observable<IAdminShare[]> {
    return this.http
      .get<IAdminShare[]>('/api/admin/shares')
      .pipe(tap((shares) => this.state.set(shares)));
  }

  /** Revokes the link. The file stays where it is; only the public way to it is removed. */
  public remove(id: string): Observable<void> {
    return this.http
      .delete<void>(`/api/admin/share/${id}`)
      .pipe(tap(() => this.state.update((list) => list.filter((share) => share.id !== id))));
  }
}
