import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { IEmailSettings, IUpdateEmailSettings } from '@models/IEmailSettings';
import { Observable } from 'rxjs';

const url = '/api/admin/email';

/**
 * The SMTP settings the whole account lifecycle rides on: without them an invitation never
 * arrives, and an invitation is the only way an account is created.
 *
 * No cached signal here — one screen reads these, and it wants what the server has right now
 * rather than what it had when the admin area opened.
 */
@Injectable({ providedIn: 'root' })
export class EmailSettingsService {
  private readonly http = inject(HttpClient);

  public get(): Observable<IEmailSettings> {
    return this.http.get<IEmailSettings>(`${url}/settings`);
  }

  /** An empty `password` keeps the stored one — see {@link IUpdateEmailSettings}. */
  public update(settings: IUpdateEmailSettings): Observable<IEmailSettings> {
    return this.http.put<IEmailSettings>(`${url}/settings`, settings);
  }

  /**
   * Sends a real message. A bad host comes back as a 502 carrying the SMTP error, which is the
   * only way the admin finds out these settings are wrong before a user does.
   */
  public sendTest(recipient: string): Observable<void> {
    return this.http.post<void>(`${url}/test`, { recipient });
  }
}
