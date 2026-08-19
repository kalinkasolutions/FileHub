import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { IFileEntry } from '@models/IFileEntry';
import { INavigation } from '@models/INavigation';
import { Observable } from 'rxjs';

/**
 * The two read routes of the browser. Both are same-origin and relative: the API serves the SPA, so
 * there is no configured base URL any more, and the session is the cookie the interceptor sends.
 */
@Injectable({ providedIn: 'root' })
export class DirectoryService {
  private readonly http = inject(HttpClient);

  /**
   * The base paths the signed-in user has been granted. An **empty array is a normal answer** — it
   * means an admin has not given this account access to anything yet, not that a call failed.
   */
  public getBasePaths(): Observable<IFileEntry[]> {
    return this.http.get<IFileEntry[]>('/api/files');
  }

  /**
   * Lists one directory. `path` is relative to the base path and carries **no leading separator**;
   * an empty one is the base path itself.
   */
  public navigate(basePathId: string, path: string): Observable<INavigation> {
    return this.http.post<INavigation>('/api/files/navigate', { basePathId, path });
  }
}
