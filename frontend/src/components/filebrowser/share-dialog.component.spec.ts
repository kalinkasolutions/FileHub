import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { AuthService } from '@services/auth.service';
import { provideToastr } from 'ngx-toastr';
import { beforeEach, describe, expect, it } from 'vitest';
import { IShareDialogData, ShareDialogComponent } from './share-dialog.component';

const data: IShareDialogData = {
  name: 'holiday.jpg',
  basePathId: 'b1',
  relativePath: 'Photos/holiday.jpg',
};

/** The one thing the dialog reads off the session: only an admin may aim a link at a group. */
class FakeAuthService {
  public readonly admin = signal(false);
  public readonly isAdmin = this.admin;
}

describe('ShareDialogComponent', () => {
  let fixture: ComponentFixture<ShareDialogComponent>;
  let http: HttpTestingController;
  let auth: FakeAuthService;

  beforeEach(() => {
    TestBed.resetTestingModule();
    auth = new FakeAuthService();

    TestBed.configureTestingModule({
      imports: [ShareDialogComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideToastr(),
        { provide: MAT_DIALOG_DATA, useValue: data },
        { provide: MatDialogRef, useValue: { close: () => undefined } },
        { provide: AuthService, useValue: auth },
      ],
    });

    http = TestBed.inject(HttpTestingController);
  });

  /** Opens the dialog as an admin, who is the only caller offered the picker. */
  function open(groups: { id: string; name: string }[]): void {
    auth.admin.set(true);
    fixture = TestBed.createComponent(ShareDialogComponent);
    fixture.detectChanges();
    http.expectOne('/api/groups').flush(groups);
    fixture.detectChanges();
  }

  /** Opens it as an ordinary account, which may only publish anonymously. */
  function openAsUser(): void {
    fixture = TestBed.createComponent(ShareDialogComponent);
    fixture.detectChanges();
  }

  // Aiming a link at a group is an access decision, so the choice is admin-only — and the groups
  // are not even fetched for anyone else.
  it('shows no picker, and asks for no groups, when the caller is not an admin', () => {
    openAsUser();

    expect(fixture.nativeElement.querySelector('#share-audience')).toBeNull();
    http.expectNone('/api/groups');
  });

  it('creates an anonymous link for an ordinary account', () => {
    openAsUser();

    fixture.componentInstance.create();

    const request = http.expectOne('/api/share');
    expect(request.request.body.audienceGroupId).toBeNull();
  });

  // An install with no groups has nothing to choose between, so the choice is not offered at all.
  it('shows no picker when there are no groups', () => {
    open([]);

    expect(fixture.nativeElement.querySelector('#share-audience')).toBeNull();
  });

  it('offers an admin the groups on top of the anonymous default', () => {
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
    auth.admin.set(true);
    fixture = TestBed.createComponent(ShareDialogComponent);
    fixture.detectChanges();
    http.expectOne('/api/groups').error(new ProgressEvent('failed'));
    fixture.detectChanges();

    expect(fixture.componentInstance.groups()).toEqual([]);
    expect(fixture.nativeElement.querySelector('#share-audience')).toBeNull();
  });
});
