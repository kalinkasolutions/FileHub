import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { provideToastr } from 'ngx-toastr';
import { beforeEach, describe, expect, it } from 'vitest';
import { PublicShareComponent } from './publicshare.component';

const shareId = '2f1d4a2c-8f4f-4a5f-9f0e-0e2b0f5f1a11';

describe('PublicShareComponent', () => {
  let fixture: ComponentFixture<PublicShareComponent>;
  let http: HttpTestingController;
  let navigations: unknown[][];

  beforeEach(() => {
    TestBed.resetTestingModule();
    navigations = [];

    TestBed.configureTestingModule({
      imports: [PublicShareComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideToastr(),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: new Map([['id', shareId]]) } },
        },
        {
          provide: Router,
          useValue: {
            navigate: (commands: unknown[]) => {
              navigations.push(commands);
              return Promise.resolve(true);
            },
          },
        },
      ],
    });

    fixture = TestBed.createComponent(PublicShareComponent);
    http = TestBed.inject(HttpTestingController);
  });

  it('reads the link through the anonymous half of the API', async () => {
    fixture.detectChanges();

    const request = http.expectOne(`/public-api/share/${shareId}`);
    expect(request.request.method).toBe('GET');
    request.flush({ id: shareId, name: 'holiday.zip', size: 1500, isDir: false });
    await fixture.whenStable();

    expect(fixture.componentInstance.share()?.name).toBe('holiday.zip');
    expect(fixture.componentInstance.loading()).toBe(false);
    expect(fixture.componentInstance.downloadUrl()).toBe(
      `${location.origin}/public-api/share/${shareId}/download`,
    );
    expect(navigations).toEqual([]);
  });

  it('sends a dead link to the 404 screen rather than reporting an error', async () => {
    fixture.detectChanges();

    http
      .expectOne(`/public-api/share/${shareId}`)
      .flush(
        { detail: 'This link is no longer available.' },
        { status: 404, statusText: 'Not Found' },
      );
    await fixture.whenStable();

    expect(fixture.componentInstance.share()).toBeNull();
    expect(fixture.componentInstance.loading()).toBe(false);
    expect(navigations).toEqual([['/404']]);
  });
});
