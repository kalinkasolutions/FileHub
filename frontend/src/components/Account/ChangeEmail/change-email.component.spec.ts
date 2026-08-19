import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideToastr } from 'ngx-toastr';
import { beforeEach, describe, expect, it } from 'vitest';
import { ChangeEmailComponent } from './change-email.component';

describe('ChangeEmailComponent', () => {
  let component: ChangeEmailComponent;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [ChangeEmailComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideToastr()],
    });

    component = TestBed.createComponent(ChangeEmailComponent).componentInstance;
    http = TestBed.inject(HttpTestingController);
  });

  it('needs an address and the current password', () => {
    expect(component.canSubmit()).toBe(false);

    component.email.set('new@example.com');
    expect(component.canSubmit()).toBe(false);

    component.currentPassword.set('secret-password');
    expect(component.canSubmit()).toBe(true);
  });

  it('says which address is waiting, and does not clear the field until it is sent', async () => {
    component.email.set('new@example.com');
    component.currentPassword.set('secret-password');

    const submitted = component.submit();
    const request = http.expectOne('/api/account/email');
    expect(request.request.body).toEqual({
      email: 'new@example.com',
      currentPassword: 'secret-password',
    });

    request.flush(null);
    await submitted;

    expect(component.pending()).toBe('new@example.com');
    expect(component.failed()).toBeNull();
    expect(component.currentPassword()).toBe('');
  });

  it('states a mail failure on screen — nothing else can complete the change', async () => {
    component.email.set('new@example.com');
    component.currentPassword.set('secret-password');

    const submitted = component.submit();
    http
      .expectOne('/api/account/email')
      .flush(
        { detail: 'The confirmation email could not be sent.' },
        { status: 502, statusText: 'Bad Gateway' },
      );
    await submitted;

    expect(component.failed()).toBe('The confirmation email could not be sent.');
    expect(component.pending()).toBeNull();
  });
});
