import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { provideToastr } from 'ngx-toastr';
import { beforeEach, describe, expect, it } from 'vitest';
import { IShareDialogData, ShareDialogComponent } from './share-dialog.component';

const data: IShareDialogData = {
  name: 'holiday.jpg',
  basePathId: 'b1',
  relativePath: 'Photos/holiday.jpg',
};

describe('ShareDialogComponent', () => {
  let fixture: ComponentFixture<ShareDialogComponent>;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.resetTestingModule();

    TestBed.configureTestingModule({
      imports: [ShareDialogComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideToastr(),
        { provide: MAT_DIALOG_DATA, useValue: data },
        { provide: MatDialogRef, useValue: { close: () => undefined } },
      ],
    });

    fixture = TestBed.createComponent(ShareDialogComponent);
    http = TestBed.inject(HttpTestingController);
  });

  function open(groups: { id: string; name: string }[]): void {
    fixture.detectChanges();
    http.expectOne('/api/groups').flush(groups);
    fixture.detectChanges();
  }

  // A caller in no group has nothing to choose between, so the choice is not offered at all.
  it('shows no picker when the caller belongs to no group', () => {
    open([]);

    expect(fixture.nativeElement.querySelector('#share-audience')).toBeNull();
  });

  it('offers the caller their own groups on top of the anonymous default', () => {
    open([{ id: 'g1', name: 'Family' }]);

    const options = Array.from<HTMLOptionElement>(
      fixture.nativeElement.querySelectorAll('#share-audience option'),
    ).map((option) => option.textContent?.trim());

    expect(options).toEqual(['Anyone with the link', 'Members of Family']);
  });

  it('creates an anonymous link by default, even with groups to choose from', () => {
    open([{ id: 'g1', name: 'Family' }]);

    fixture.componentInstance.create();

    const request = http.expectOne('/api/share');
    expect(request.request.body.audienceGroupId).toBeNull();
  });

  it('aims the link at the chosen group', () => {
    open([{ id: 'g1', name: 'Family' }]);

    fixture.componentInstance.setAudience('g1');
    fixture.componentInstance.create();

    const request = http.expectOne('/api/share');
    expect(request.request.body.audienceGroupId).toBe('g1');
  });

  // The one thing a group-aimed URL cannot say about itself: it is dead in anyone else's hands.
  it('says the link is no longer anonymous once a group is chosen', () => {
    open([{ id: 'g1', name: 'Family' }]);
    expect(fixture.nativeElement.textContent).toContain('Anyone with this link can download it');

    fixture.componentInstance.setAudience('g1');
    fixture.detectChanges();

    const note = fixture.nativeElement.querySelector('.audience-note');
    expect(note.classList).toContain('restricted');
    expect(note.textContent).toContain('Only signed-in members of');
    expect(note.textContent).toContain('Family');
    expect(note.textContent).toContain('dead link');
  });

  // The dialog predates groups and has to keep working when the route fails or is not reachable.
  it('falls back to the anonymous-only dialog when the groups cannot be read', () => {
    fixture.detectChanges();
    http.expectOne('/api/groups').error(new ProgressEvent('failed'));
    fixture.detectChanges();

    expect(fixture.componentInstance.groups()).toEqual([]);
    expect(fixture.nativeElement.querySelector('#share-audience')).toBeNull();
  });
});
