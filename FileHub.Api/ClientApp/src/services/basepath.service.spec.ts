import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { IBasePath } from '@models/IBasePath';
import { BasePathService } from '@services/basepath.service';
import { beforeEach, describe, expect, it } from 'vitest';

function basePath(overrides: Partial<IBasePath> = {}): IBasePath {
  return {
    id: '1',
    path: '/mnt/media',
    name: 'Media',
    createdAt: '2026-01-01T00:00:00Z',
    userCount: 0,
    groupCount: 0,
    ...overrides,
  };
}

describe('BasePathService', () => {
  let service: BasePathService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(BasePathService);
    http = TestBed.inject(HttpTestingController);
  });

  it('publishes the loaded list as a signal', () => {
    service.load().subscribe();
    http.expectOne('/api/admin/base-path').flush([basePath()]);

    expect(service.basePaths().map((p) => p.name)).toEqual(['Media']);
  });

  it('appends a created base path without re-reading the list', () => {
    service.load().subscribe();
    http.expectOne('/api/admin/base-path').flush([basePath()]);

    service.create({ path: '/mnt/backup', name: '' }).subscribe();
    http.expectOne('/api/admin/base-path').flush(basePath({ id: '2', name: 'backup' }));

    expect(service.basePaths().map((p) => p.id)).toEqual(['1', '2']);
  });

  it('replaces the edited row in place', () => {
    service.load().subscribe();
    http.expectOne('/api/admin/base-path').flush([basePath(), basePath({ id: '2' })]);

    service.update('2', { path: '/mnt/other', name: 'Other' }).subscribe();
    http.expectOne('/api/admin/base-path/2').flush(basePath({ id: '2', name: 'Other' }));

    expect(service.basePaths().map((p) => p.name)).toEqual(['Media', 'Other']);
  });

  it('drops the deleted row', () => {
    service.load().subscribe();
    http.expectOne('/api/admin/base-path').flush([basePath(), basePath({ id: '2' })]);

    service.remove('1').subscribe();
    http.expectOne('/api/admin/base-path/1').flush(null);

    expect(service.basePaths().map((p) => p.id)).toEqual(['2']);
  });

  it('re-reads the list after a grant, because that is what moves userCount', () => {
    service.load().subscribe();
    http.expectOne('/api/admin/base-path').flush([basePath()]);

    service.setUsers('1', ['u1', 'u2']).subscribe();
    const put = http.expectOne('/api/admin/base-path/1/users');
    expect(put.request.method).toBe('PUT');
    expect(put.request.body).toEqual({ userIds: ['u1', 'u2'] });
    put.flush(null);

    http.expectOne('/api/admin/base-path').flush([basePath({ userCount: 2 })]);
    expect(service.basePaths()[0].userCount).toBe(2);
  });

  it('re-reads the list after a group grant, because that is what moves groupCount', () => {
    service.load().subscribe();
    http.expectOne('/api/admin/base-path').flush([basePath()]);

    service.setGroups('1', ['g1']).subscribe();
    const put = http.expectOne('/api/admin/base-path/1/groups');
    expect(put.request.method).toBe('PUT');
    expect(put.request.body).toEqual({ groupIds: ['g1'] });
    put.flush(null);

    http.expectOne('/api/admin/base-path').flush([basePath({ groupCount: 1 })]);
    expect(service.basePaths()[0].groupCount).toBe(1);
  });

  it('reads the granted groups as a bare id array', () => {
    service.getGroups('1').subscribe((ids) => expect(ids).toEqual(['g1']));
    http.expectOne('/api/admin/base-path/1/groups').flush(['g1']);
  });

  it('edits the same grant table from the user side', () => {
    service.setBasePathsOfUser('u1', ['1']).subscribe();

    const put = http.expectOne('/api/admin/users/u1/base-paths');
    expect(put.request.body).toEqual({ basePathIds: ['1'] });
    put.flush(null);

    http.expectOne('/api/admin/base-path').flush([basePath({ userCount: 1 })]);
    expect(service.basePaths()[0].userCount).toBe(1);
  });
});
