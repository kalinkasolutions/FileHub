import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { IAuthStatus } from '@models/IAuthStatus';
import { AuthService } from '@services/auth.service';
import { beforeEach, describe, expect, it } from 'vitest';

/**
 * What the app reads off `GET /api/auth/status`. The interesting part is `canCreateShares`: the
 * server sends the roles an account *acts with*, so `Admin` arrives with `CreateShares` already
 * beside it and the client does not re-derive the implication.
 */
describe('AuthService', () => {
  let service: AuthService;
  let http: HttpTestingController;

  const status = (roles: string[]): IAuthStatus => ({
    authenticated: true,
    userId: 'u1',
    username: 'Alice',
    email: 'alice@example.com',
    roles,
    mustChangePassword: false,
  });

  const load = (roles: string[]): void => {
    service.ensureLoaded().subscribe();
    http.expectOne('/api/auth/status').flush(status(roles));
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  it('offers the share controls to an account holding the role', () => {
    load(['User', 'CreateShares']);

    expect(service.canCreateShares()).toBe(true);
    expect(service.isAdmin()).toBe(false);
  });

  it('withholds them from an account that only browses', () => {
    load(['User']);

    expect(service.canCreateShares()).toBe(false);
  });

  it('offers them to an admin, whose implied roles the server has already expanded', () => {
    load(['Admin', 'User', 'CreateShares']);

    expect(service.canCreateShares()).toBe(true);
    expect(service.isAdmin()).toBe(true);
  });

  it('withholds them before a status has been loaded, and from a signed-out caller', () => {
    expect(service.canCreateShares()).toBe(false);

    service.ensureLoaded().subscribe();
    http.expectOne('/api/auth/status').flush({
      authenticated: false,
      userId: null,
      username: null,
      email: null,
      roles: [],
      mustChangePassword: false,
    });

    expect(service.canCreateShares()).toBe(false);
  });
});
