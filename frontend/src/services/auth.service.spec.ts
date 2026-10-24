import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { IAuthStatus } from '@models/IAuthStatus';
import { AuthService } from '@services/auth.service';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

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

  /**
   * A reload during a large download can find the status request failing or stalled. That is the
   * connection, not the session, and must never read as signed out.
   */
  describe('when the status request does not get through', () => {
    const networkError = new ProgressEvent('error');
    let emitted: (IAuthStatus | null)[];

    const subscribe = (): void => {
      service.ensureLoaded().subscribe((value) => emitted.push(value));
    };

    beforeEach(() => {
      vi.useFakeTimers();
      emitted = [];
    });

    afterEach(() => {
      vi.useRealTimers();
    });

    it('asks again after a network error and takes the answer', () => {
      subscribe();
      http.expectOne('/api/auth/status').error(networkError);

      vi.advanceTimersByTime(500);
      http.expectOne('/api/auth/status').flush(status(['User']));

      expect(emitted).toEqual([status(['User'])]);
      expect(service.isAuthenticated()).toBe(true);
    });

    it('gives up on a stalled request and asks again', () => {
      subscribe();
      const stalled = http.expectOne('/api/auth/status');

      vi.advanceTimersByTime(5_000);
      expect(stalled.cancelled).toBe(true);

      vi.advanceTimersByTime(500);
      http.expectOne('/api/auth/status').flush(status(['User']));

      expect(service.isAuthenticated()).toBe(true);
    });

    it('answers null without caching anything once every attempt has failed', () => {
      subscribe();
      for (const delayBeforeNext of [500, 1_000, 2_000, 0]) {
        http.expectOne('/api/auth/status').error(networkError);
        vi.advanceTimersByTime(delayBeforeNext);
      }

      expect(emitted).toEqual([null]);
      expect(service.status()).toBeNull();

      service.ensureLoaded().subscribe();
      http.expectOne('/api/auth/status');
    });
  });
});
