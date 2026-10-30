import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { IMediaInfo, IMediaInfoTarget } from '@models/IMediaInfo';
import { Observable, tap } from 'rxjs';

const url = '/api/media';

/**
 * The info behind a folder's info button. The server caches every answer in its database; this
 * keeps the ones this session has seen as well, so reopening a popover costs no request at all.
 */
@Injectable({ providedIn: 'root' })
export class MediaInfoService {
  private readonly http = inject(HttpClient);
  private readonly seen = new Map<string, IMediaInfo>();

  /** `GET /api/media/info`: the cached answer, fetched from the source on the first ask. */
  public get(target: IMediaInfoTarget): Observable<IMediaInfo> {
    return this.http
      .get<IMediaInfo>(`${url}/info`, { params: params(target) })
      .pipe(tap((info) => this.seen.set(key(target), info)));
  }

  /** What this session already holds for the folder, or null. */
  public known(target: IMediaInfoTarget): IMediaInfo | null {
    return this.seen.get(key(target)) ?? null;
  }

  /** `POST /api/media/info/refresh`: ask the source again. Admin-only on the server. */
  public refresh(target: IMediaInfoTarget): Observable<IMediaInfo> {
    return this.http
      .post<IMediaInfo>(`${url}/info/refresh`, null, { params: params(target) })
      .pipe(tap((info) => this.seen.set(key(target), info)));
  }

  /** `GET /api/media/poster/{id}`, same-origin like every other request. */
  public posterUrl(id: string): string {
    return `${url}/poster/${encodeURIComponent(id)}`;
  }
}

function params(target: IMediaInfoTarget): HttpParams {
  return new HttpParams().set('basePathId', target.basePathId).set('path', target.relativePath);
}

function key(target: IMediaInfoTarget): string {
  return `${target.basePathId}:${target.relativePath}`;
}
