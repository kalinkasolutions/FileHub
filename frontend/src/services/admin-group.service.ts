import { HttpClient } from '@angular/common/http';
import { Injectable, Signal, inject, signal } from '@angular/core';
import { IGroup, ISaveGroup, sortGroups } from '@models/IGroup';
import { Observable, switchMap, tap } from 'rxjs';

const url = '/api/admin/groups';

/**
 * Groups as the admin edits them: the named sets of accounts that base paths are granted to.
 * Admin-only, all of it — `group.service.ts` is the read-only list an ordinary user gets for
 * picking the audience of a share, and it is a different route.
 *
 * Both membership and the base-path grant are **replaced** by their PUT, not merged: an id left out
 * of the list is a revocation, and the API deletes the share links that revocation orphans. Both
 * also move a count on the row, so saving one re-reads the list rather than guessing at the number.
 */
@Injectable({ providedIn: 'root' })
export class AdminGroupService {
  private readonly http = inject(HttpClient);

  private readonly state = signal<IGroup[]>([]);

  public readonly groups: Signal<IGroup[]> = this.state.asReadonly();

  public load(): Observable<IGroup[]> {
    return this.http.get<IGroup[]>(url).pipe(tap((groups) => this.state.set(sortGroups(groups))));
  }

  public create(group: ISaveGroup): Observable<IGroup> {
    return this.http
      .post<IGroup>(url, group)
      .pipe(tap((created) => this.state.update((list) => sortGroups([...list, created]))));
  }

  public rename(id: string, group: ISaveGroup): Observable<IGroup> {
    return this.http
      .put<IGroup>(`${url}/${id}`, group)
      .pipe(tap((updated) => this.state.update((list) => list.map(replace(updated)))));
  }

  /** Deleting a group takes its grants, its memberships *and* every link aimed at it. */
  public remove(id: string): Observable<void> {
    return this.http
      .delete<void>(`${url}/${id}`)
      .pipe(tap(() => this.state.update((list) => list.filter((group) => group.id !== id))));
  }

  /** The ids of the accounts in one group. */
  public getMembers(id: string): Observable<string[]> {
    return this.http.get<string[]>(`${url}/${id}/members`);
  }

  public setMembers(id: string, userIds: string[]): Observable<IGroup[]> {
    return this.http
      .put<void>(`${url}/${id}/members`, { userIds })
      .pipe(switchMap(() => this.load()));
  }

  /** The ids of the base paths this group grants its members. */
  public getBasePaths(id: string): Observable<string[]> {
    return this.http.get<string[]>(`${url}/${id}/base-paths`);
  }

  public setBasePaths(id: string, basePathIds: string[]): Observable<IGroup[]> {
    return this.http
      .put<void>(`${url}/${id}/base-paths`, { basePathIds })
      .pipe(switchMap(() => this.load()));
  }
}

function replace(updated: IGroup): (group: IGroup) => IGroup {
  return (group) => (group.id === updated.id ? updated : group);
}
