import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { IMediaInfo } from '@models/IMediaInfo';
import { MediaInfoService } from '@services/media-info.service';
import { beforeEach, describe, expect, it } from 'vitest';

const dune: IMediaInfo = {
  id: 'm1',
  found: true,
  query: 'Dune Part Two',
  source: 'tmdb',
  mediaType: 'movie',
  title: 'Dune: Part Two',
  year: 2024,
  overview: '',
  rating: 8.2,
  genres: ['Science Fiction'],
  platforms: [],
  hasPoster: true,
  externalUrl: 'https://www.themoviedb.org/movie/693134',
  fetchedAt: '2026-01-01T00:00:00Z',
};

const target = { basePathId: 'b', relativePath: 'Dune Part Two (2024)', name: 'Dune Part Two (2024)' };

describe('MediaInfoService', () => {
  let service: MediaInfoService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(MediaInfoService);
    http = TestBed.inject(HttpTestingController);
  });

  it('asks for the folder by base path and relative path', () => {
    service.get(target).subscribe();

    const request = http.expectOne((r) => r.url === '/api/media/info');
    expect(request.request.params.get('basePathId')).toBe('b');
    expect(request.request.params.get('path')).toBe('Dune Part Two (2024)');
  });

  it('remembers an answer for the session, so a reopened popover costs nothing', () => {
    expect(service.known(target)).toBeNull();

    service.get(target).subscribe();
    http.expectOne((r) => r.url === '/api/media/info').flush(dune);

    expect(service.known(target)?.title).toBe('Dune: Part Two');
  });

  it('refreshes through the admin route and replaces what it remembered', () => {
    service.get(target).subscribe();
    http.expectOne((r) => r.url === '/api/media/info').flush(dune);

    service.refresh(target).subscribe();
    const request = http.expectOne((r) => r.url === '/api/media/info/refresh');
    expect(request.request.method).toBe('POST');
    request.flush({ ...dune, rating: 8.5 });

    expect(service.known(target)?.rating).toBe(8.5);
  });

  it('builds the poster url from the id', () => {
    expect(service.posterUrl('m1')).toBe('/api/media/poster/m1');
  });
});
