import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { ICreateShare, IShareLink } from '@models/IShareLink';
import { Observable } from 'rxjs';

/**
 * Public links. Creating and revoking one needs a cookie; redeeming one does not, which is why the
 * landing page's call goes to `public-api` instead.
 */
@Injectable({ providedIn: 'root' })
export class ShareService {
  private readonly http = inject(HttpClient);

  /** The created link carries its absolute URL in `link`, stamped by the API. */
  public create(data: ICreateShare): Observable<IShareLink> {
    return this.http.post<IShareLink>('/api/share', data);
  }

  /** The caller's **own** links. A non-admin has no other way to see or revoke what they shared. */
  public list(): Observable<IShareLink[]> {
    return this.http.get<IShareLink[]>('/api/share');
  }

  public revoke(id: string): Observable<void> {
    return this.http.delete<void>(`/api/share/${id}`);
  }
}
