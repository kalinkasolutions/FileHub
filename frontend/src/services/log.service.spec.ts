import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { LogService } from '@services/log.service';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';

describe('LogService', () => {
  let service: LogService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(LogService);
    http = TestBed.inject(HttpTestingController);
  });

  // An empty filter has to be an empty query string, not a row of blank parameters — otherwise
  // "no filter" is something the server has to decide to ignore.
  it('sends no parameters when nothing is filtered', () => {
    service.query({}).subscribe();

    const request = http.expectOne((r) => r.url === '/api/admin/logs');
    expect(request.request.params.keys()).toEqual([]);
    request.flush({ entries: [], totalCount: 0, hasMore: false });
  });

  it('sends each filter it was given', () => {
    service
      .query({
        minLevel: 'Warning',
        search: 'deleted group',
        from: '2026-08-31T00:00:00.000Z',
        to: '2026-08-31T23:59:59.000Z',
        take: 50,
      })
      .subscribe();

    const request = http.expectOne((r) => r.url === '/api/admin/logs');
    expect(request.request.params.get('minLevel')).toBe('Warning');
    expect(request.request.params.get('search')).toBe('deleted group');
    expect(request.request.params.get('from')).toBe('2026-08-31T00:00:00.000Z');
    expect(request.request.params.get('to')).toBe('2026-08-31T23:59:59.000Z');
    expect(request.request.params.get('take')).toBe('50');
    request.flush({ entries: [], totalCount: 0, hasMore: false });
  });

  it('sends afterId when tailing', () => {
    service.query({ afterId: 42 }).subscribe();

    const request = http.expectOne((r) => r.url === '/api/admin/logs');
    expect(request.request.params.get('afterId')).toBe('42');
    request.flush({ entries: [], totalCount: 0, hasMore: false });
  });

  // Id 0 is not a row. Sending it would ask the server for "everything after nothing", which is a
  // different query from the one the screen means on its first load.
  it('leaves afterId off when there is nothing in hand yet', () => {
    service.query({ afterId: 0 }).subscribe();

    const request = http.expectOne((r) => r.url === '/api/admin/logs');
    expect(request.request.params.has('afterId')).toBe(false);
    request.flush({ entries: [], totalCount: 0, hasMore: false });
  });

  it('drops an empty search rather than sending a blank filter', () => {
    service.query({ search: '', minLevel: '' }).subscribe();

    const request = http.expectOne((r) => r.url === '/api/admin/logs');
    expect(request.request.params.keys()).toEqual([]);
    request.flush({ entries: [], totalCount: 0, hasMore: false });
  });

  afterEach(() => http.verify());
});
