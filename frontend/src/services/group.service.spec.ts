import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { GroupService } from '@services/group.service';
import { beforeEach, describe, expect, it } from 'vitest';

describe('GroupService', () => {
  let service: GroupService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(GroupService);
    http = TestBed.inject(HttpTestingController);
  });

  // The caller-facing route, not the admin one: an ordinary user has to be able to pick an audience
  // without being handed the whole group list of the install.
  it('reads the caller-facing route', () => {
    let groups: { id: string; name: string }[] = [];
    service.list().subscribe((x) => (groups = x));

    const request = http.expectOne('/api/groups');
    expect(request.request.method).toBe('GET');
    request.flush([{ id: 'g1', name: 'Family' }]);

    expect(groups).toEqual([{ id: 'g1', name: 'Family' }]);
  });

  it('passes an empty list through — a caller in no group is the ordinary case', () => {
    let groups: { id: string; name: string }[] | null = null;
    service.list().subscribe((x) => (groups = x));

    http.expectOne('/api/groups').flush([]);

    expect(groups).toEqual([]);
  });
});
