import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { skipLoadingOverlay } from '@interceptors/loading.interceptor';
import { VersionService } from '@services/version.service';
import { beforeEach, describe, expect, it } from 'vitest';

describe('VersionService', () => {
  let service: VersionService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(VersionService);
    http = TestBed.inject(HttpTestingController);
  });

  it('reads version.json and keeps it', async () => {
    const loading = service.ensureLoaded();

    const request = http.expectOne('/version.json');
    expect(request.request.method).toBe('GET');
    // Nobody asked for this request, so it must not raise the full-screen overlay.
    expect(request.request.context.get(skipLoadingOverlay)).toBe(true);
    request.flush({ version: 'v1.4.0', commitSha: 'abc1234def', builtAt: '2026-08-31T10:00:00Z' });

    await loading;

    expect(service.version()?.version).toBe('v1.4.0');
    expect(service.loaded()).toBe(true);
  });

  // A local build has no version.json at all. That is the ordinary development state, not an error
  // to report, so it has to settle as "no version" rather than leaving the screen loading forever.
  it('treats a missing file as no version', async () => {
    const loading = service.ensureLoaded();

    http.expectOne('/version.json').flush('', { status: 404, statusText: 'Not Found' });

    await loading;

    expect(service.version()).toBeNull();
    expect(service.loaded()).toBe(true);
  });

  // A plain `docker build` passes no build args, so the file exists with every field empty. That is
  // the same thing as having no release, and the screen must not offer to link an empty commit.
  it('treats an empty tag as no version', async () => {
    const loading = service.ensureLoaded();

    http.expectOne('/version.json').flush({ version: '', commitSha: '', builtAt: '' });

    await loading;

    expect(service.version()).toBeNull();
  });

  it('asks once however many callers there are', async () => {
    const first = service.ensureLoaded();
    const second = service.ensureLoaded();

    http.expectOne('/version.json').flush({ version: 'v2', commitSha: '', builtAt: '' });

    await Promise.all([first, second]);

    // The one request above was the only one; a second would fail this.
    http.verify();
    expect(service.version()?.version).toBe('v2');
  });
});
