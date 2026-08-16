import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { IAdminShare } from '@models/IAdminShare';
import { IShare } from '@models/IShare';
import { ICreateShare, IShareLink } from '@models/IShareLink';
import { Observable, map } from 'rxjs';

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

  // ─── For the screens that are still pre-rewrite ───────────────────────────
  // The public landing page and the admin share list. Both keep their old signatures so they go on
  // compiling; only the URLs are corrected to the .NET routes. Move them out when those screens are
  // rebuilt — nothing in the browsing UI calls them.

  /** @deprecated Used by the public share landing page. */
  public validateShare(id: string): Observable<IShare> {
    return this.http.get<IShare>(`/public-api/share/${id}`);
  }

  /** @deprecated Used by the admin share list; every user's links, not just the caller's. */
  public getShares(): Observable<IAdminShare[]> {
    return this.http.get<IAdminShare[]>('/api/admin/shares');
  }

  /** @deprecated Used by the admin share list. The route answers with no content, so the caller's
   * own row is echoed back to keep its `subscribe(deleted => …)` working. */
  public delete(share: IAdminShare): Observable<IAdminShare> {
    return this.http.delete<void>(`/api/admin/share/${share.Id}`).pipe(map(() => share));
  }
}
