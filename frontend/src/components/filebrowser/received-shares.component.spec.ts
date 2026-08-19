import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { IReceivedShare } from '@models/IReceivedShare';
import { provideToastr } from 'ngx-toastr';
import { beforeEach, describe, expect, it } from 'vitest';
import { ReceivedSharesComponent } from './received-shares.component';

const share: IReceivedShare = {
  id: 's1',
  name: 'holiday.jpg',
  isDir: false,
  size: 2000,
  downloadCount: 1,
  maxDownloadCount: 3,
  createdAt: '2026-05-04T10:00:00Z',
  audienceGroupId: 'g1',
  audienceGroupName: 'Family',
  sharedBy: 'kim',
  link: 'https://files.example.com/share/s1',
};

describe('ReceivedSharesComponent', () => {
  let fixture: ComponentFixture<ReceivedSharesComponent>;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.resetTestingModule();

    TestBed.configureTestingModule({
      imports: [ReceivedSharesComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideToastr()],
    });

    fixture = TestBed.createComponent(ReceivedSharesComponent);
    http = TestBed.inject(HttpTestingController);
  });

  function load(shares: IReceivedShare[]): void {
    fixture.detectChanges();
    http.expectOne('/api/share/received').flush(shares);
    fixture.detectChanges();
  }

  it('says what the tab is for when nothing has been shared with the caller', () => {
    load([]);

    // An account in no group always lands here, so the empty state has to read as an answer rather
    // than as a list that failed to arrive.
    expect(fixture.nativeElement.textContent).toContain('Nothing yet');
    expect(fixture.nativeElement.querySelectorAll('.link')).toHaveLength(0);
  });

  it('names the group and who shared it', () => {
    load([share]);

    const row = fixture.nativeElement.querySelector('.link');
    expect(row.querySelector('.name').textContent).toContain('holiday.jpg');
    expect(row.querySelector('.audience').textContent).toContain('Family');
    expect(row.querySelector('.meta').textContent).toContain('from kim');
    expect(row.querySelector('.meta').textContent).toContain('1 / 3 downloads');
  });

  // Every link in this list is aimed at a group, so all of them wear the mark — there is no
  // anonymous row here to tell them apart from.
  it('marks every row as restricted', () => {
    load([share, { ...share, id: 's2', audienceGroupName: 'Work' }]);

    const rows = Array.from<HTMLElement>(fixture.nativeElement.querySelectorAll('.link'));
    expect(rows).toHaveLength(2);
    expect(rows.every((row) => row.classList.contains('restricted'))).toBe(true);
  });

  // Revoking somebody else's link is not this screen's to offer: the creator or an admin does that.
  it('offers no way to revoke', () => {
    load([share]);

    expect(fixture.nativeElement.querySelector('.icon-btn.danger')).toBeNull();
  });
});
