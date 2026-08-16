import { HttpClient } from '@angular/common/http';
import { Injectable, Signal, inject, signal } from '@angular/core';
import { IBasePath, ISaveBasePath } from '@models/IBasePath';
import { Observable, switchMap, tap } from 'rxjs';

const url = '/api/admin/base-path';

/**
 * The base paths and the grants that make one visible. Admin-only, all of it.
 *
 * The list is held in a signal rather than re-fetched by every screen that shows it: the base
 * paths are the options in the per-user grant editor as well as the rows of their own section,
 * so two screens read the same collection.
 *
 * Both grant routes replace the whole set — an id left out is a revocation — and both change a
 * `userCount`, so saving one re-reads the list rather than guessing at the new number.
 */
@Injectable({ providedIn: 'root' })
export class BasePathService {
  private readonly http = inject(HttpClient);

  private readonly state = signal<IBasePath[]>([]);

  public readonly basePaths: Signal<IBasePath[]> = this.state.asReadonly();

  public load(): Observable<IBasePath[]> {
    return this.http.get<IBasePath[]>(url).pipe(tap((basePaths) => this.state.set(basePaths)));
  }

  public create(basePath: ISaveBasePath): Observable<IBasePath> {
    return this.http
      .post<IBasePath>(url, basePath)
      .pipe(tap((created) => this.state.update((list) => [...list, created])));
  }

  public update(id: string, basePath: ISaveBasePath): Observable<IBasePath> {
    return this.http
      .put<IBasePath>(`${url}/${id}`, basePath)
      .pipe(tap((updated) => this.state.update((list) => list.map(replace(updated)))));
  }

  /** Deleting a base path takes its grants *and* every share link into it with it. */
  public remove(id: string): Observable<void> {
    return this.http
      .delete<void>(`${url}/${id}`)
      .pipe(tap(() => this.state.update((list) => list.filter((basePath) => basePath.id !== id))));
  }

  /** The ids of the users who may see one base path. */
  public getUsers(id: string): Observable<string[]> {
    return this.http.get<string[]>(`${url}/${id}/users`);
  }

  public setUsers(id: string, userIds: string[]): Observable<IBasePath[]> {
    return this.http
      .put<void>(`${url}/${id}/users`, { userIds })
      .pipe(switchMap(() => this.load()));
  }

  /** The same grant table from the other side: the base paths one user may see. */
  public getBasePathsOfUser(userId: string): Observable<string[]> {
    return this.http.get<string[]>(`/api/admin/users/${userId}/base-paths`);
  }

  public setBasePathsOfUser(userId: string, basePathIds: string[]): Observable<IBasePath[]> {
    return this.http
      .put<void>(`/api/admin/users/${userId}/base-paths`, { basePathIds })
      .pipe(switchMap(() => this.load()));
  }
}

function replace(updated: IBasePath): (basePath: IBasePath) => IBasePath {
  return (basePath) => (basePath.id === updated.id ? updated : basePath);
}
