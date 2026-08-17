import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { IGroupSummary } from '@models/IGroupSummary';
import { Observable } from 'rxjs';

/**
 * The groups the signed-in caller may aim a share link at — their own, or every group when the
 * caller is an admin. Read-only on purpose: creating a group and deciding who is in it is part of
 * the access model and lives behind the admin role, in the admin area's own service.
 */
@Injectable({ providedIn: 'root' })
export class GroupService {
  private readonly http = inject(HttpClient);

  public list(): Observable<IGroupSummary[]> {
    return this.http.get<IGroupSummary[]>('/api/groups');
  }
}
