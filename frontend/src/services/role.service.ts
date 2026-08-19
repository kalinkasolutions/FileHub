import { HttpClient } from '@angular/common/http';
import { Injectable, Signal, inject, signal } from '@angular/core';
import { IRole } from '@models/IRole';
import { Observable, tap } from 'rxjs';

/**
 * The fixed roles and how many accounts hold each. Read-only by design: the names come from the
 * backend's constants, which are what the authorization policies check, so there is nothing here
 * to create or delete — role assignment happens on the user.
 */
@Injectable({ providedIn: 'root' })
export class RoleService {
  private readonly http = inject(HttpClient);

  private readonly state = signal<IRole[]>([]);

  public readonly roles: Signal<IRole[]> = this.state.asReadonly();

  public load(): Observable<IRole[]> {
    return this.http.get<IRole[]>('/api/admin/roles').pipe(tap((roles) => this.state.set(roles)));
  }
}
