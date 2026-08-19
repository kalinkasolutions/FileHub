import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ShareService } from '@services/share.service';
import { beforeEach, describe, expect, it } from 'vitest';

describe('ShareService', () => {
  let service: ShareService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(ShareService);
    http = TestBed.inject(HttpTestingController);
  });

  // Null is what the API reads as "anonymous by URL", which is what a link has always been.
  it('creates an anonymous link when no group was chosen', () => {
    service
      .create({ basePathId: 'b1', relativePath: 'Photos/a.jpg', maxDownloadCount: 0 })
      .subscribe();

    const request = http.expectOne('/api/share');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      basePathId: 'b1',
      relativePath: 'Photos/a.jpg',
      maxDownloadCount: 0,
    });
    request.flush({});
  });

  it('sends the audience when one was chosen', () => {
    service
      .create({
        basePathId: 'b1',
        relativePath: 'Photos/a.jpg',
        maxDownloadCount: 5,
        audienceGroupId: 'g1',
      })
      .subscribe();

    const request = http.expectOne('/api/share');
    expect(request.request.body.audienceGroupId).toBe('g1');
    request.flush({});
  });

  it('reads what the callers groups were sent from its own route', () => {
    service.received().subscribe();

    // A separate route from the caller's own links: one is what they published, the other what was
    // published to them, and no account necessarily has both.
    const request = http.expectOne('/api/share/received');
    expect(request.request.method).toBe('GET');
    request.flush([]);
  });

  it('revokes by id', () => {
    service.revoke('s1').subscribe();

    const request = http.expectOne('/api/share/s1');
    expect(request.request.method).toBe('DELETE');
    request.flush(null);
  });
});
