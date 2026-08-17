import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { IGroup } from '@models/IGroup';
import { AdminGroupService } from '@services/admin-group.service';
import { beforeEach, describe, expect, it } from 'vitest';

function group(overrides: Partial<IGroup> = {}): IGroup {
  return {
    id: '1',
    name: 'Family',
    memberCount: 0,
    basePathCount: 0,
    createdAt: '2026-01-01T00:00:00Z',
    ...overrides,
  };
}

describe('AdminGroupService', () => {
  let service: AdminGroupService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(AdminGroupService);
    http = TestBed.inject(HttpTestingController);
  });

  it('publishes the loaded list as a signal, newest first', () => {
    service.load().subscribe();
    http
      .expectOne('/api/admin/groups')
      .flush([
        group({ id: 'old', name: 'Old', createdAt: '2026-01-01T00:00:00Z' }),
        group({ id: 'new', name: 'New', createdAt: '2026-06-01T00:00:00Z' }),
      ]);

    expect(service.groups().map((g) => g.name)).toEqual(['New', 'Old']);
  });

  it('appends a created group without re-reading the list', () => {
    service.load().subscribe();
    http.expectOne('/api/admin/groups').flush([group()]);

    service.create({ name: 'Friends' }).subscribe();
    const post = http.expectOne('/api/admin/groups');
    expect(post.request.method).toBe('POST');
    expect(post.request.body).toEqual({ name: 'Friends' });
    post.flush(group({ id: '2', name: 'Friends', createdAt: '2026-02-01T00:00:00Z' }));

    expect(service.groups().map((g) => g.id)).toEqual(['2', '1']);
  });

  it('replaces the renamed row in place', () => {
    service.load().subscribe();
    http.expectOne('/api/admin/groups').flush([group(), group({ id: '2', name: 'Friends' })]);

    service.rename('2', { name: 'Colleagues' }).subscribe();
    http.expectOne('/api/admin/groups/2').flush(group({ id: '2', name: 'Colleagues' }));

    expect(service.groups().map((g) => g.name)).toEqual(['Family', 'Colleagues']);
  });

  it('drops the deleted row', () => {
    service.load().subscribe();
    http.expectOne('/api/admin/groups').flush([group(), group({ id: '2' })]);

    service.remove('1').subscribe();
    http.expectOne('/api/admin/groups/1').flush(null);

    expect(service.groups().map((g) => g.id)).toEqual(['2']);
  });

  it('replaces the membership and re-reads, because that is what moves memberCount', () => {
    service.setMembers('1', ['u1', 'u2']).subscribe();

    const put = http.expectOne('/api/admin/groups/1/members');
    expect(put.request.method).toBe('PUT');
    expect(put.request.body).toEqual({ userIds: ['u1', 'u2'] });
    put.flush(null);

    http.expectOne('/api/admin/groups').flush([group({ memberCount: 2 })]);
    expect(service.groups()[0].memberCount).toBe(2);
  });

  it('replaces the granted base paths and re-reads', () => {
    service.setBasePaths('1', ['b1']).subscribe();

    const put = http.expectOne('/api/admin/groups/1/base-paths');
    expect(put.request.method).toBe('PUT');
    expect(put.request.body).toEqual({ basePathIds: ['b1'] });
    put.flush(null);

    http.expectOne('/api/admin/groups').flush([group({ basePathCount: 1 })]);
    expect(service.groups()[0].basePathCount).toBe(1);
  });

  it('reads both lists as bare id arrays', () => {
    service.getMembers('1').subscribe((ids) => expect(ids).toEqual(['u1']));
    http.expectOne('/api/admin/groups/1/members').flush(['u1']);

    service.getBasePaths('1').subscribe((ids) => expect(ids).toEqual(['b1']));
    http.expectOne('/api/admin/groups/1/base-paths').flush(['b1']);
  });
});
