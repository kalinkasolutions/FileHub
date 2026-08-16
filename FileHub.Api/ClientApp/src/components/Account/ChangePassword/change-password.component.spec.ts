import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideToastr } from 'ngx-toastr';
import { beforeEach, describe, expect, it } from 'vitest';
import { AccountChangePasswordComponent } from './change-password.component';

function create(): AccountChangePasswordComponent {
  TestBed.configureTestingModule({
    imports: [AccountChangePasswordComponent],
    providers: [provideHttpClient(), provideHttpClientTesting(), provideToastr()],
  });

  return TestBed.createComponent(AccountChangePasswordComponent).componentInstance;
}

describe('AccountChangePasswordComponent', () => {
  let component: AccountChangePasswordComponent;

  beforeEach(() => {
    TestBed.resetTestingModule();
    component = create();
  });

  it('stays unsubmittable until all three fields agree with the API rules', () => {
    expect(component.canSubmit()).toBe(false);

    component.currentPassword.set('old-password');
    component.newPassword.set('short');
    component.confirmPassword.set('short');
    expect(component.canSubmit()).toBe(false);

    component.newPassword.set('long-enough');
    component.confirmPassword.set('long-enough');
    expect(component.canSubmit()).toBe(true);
  });

  it('will not submit without the current password', () => {
    component.newPassword.set('long-enough');
    component.confirmPassword.set('long-enough');
    expect(component.canSubmit()).toBe(false);
  });

  it('complains about the repeat only once it has been typed into', () => {
    component.newPassword.set('long-enough');
    expect(component.mismatch()).toBe(false);

    component.confirmPassword.set('l');
    expect(component.mismatch()).toBe(true);

    component.confirmPassword.set('long-enough');
    expect(component.mismatch()).toBe(false);
  });

  it('complains about length only once typing has started', () => {
    expect(component.tooShort()).toBe(false);

    component.newPassword.set('short');
    expect(component.tooShort()).toBe(true);

    component.newPassword.set('long-enough');
    expect(component.tooShort()).toBe(false);
  });

  it('sends all three fields — the DTO requires the repeat and compares it server-side', async () => {
    component.currentPassword.set('old-password');
    component.newPassword.set('long-enough');
    component.confirmPassword.set('long-enough');

    const submitted = component.submit();

    const http = TestBed.inject(HttpTestingController);
    const request = http.expectOne('/api/account/password');
    expect(request.request.body).toEqual({
      currentPassword: 'old-password',
      newPassword: 'long-enough',
      confirmPassword: 'long-enough',
    });

    request.flush(null);
    // Changing the password clears a forced change, so the status is re-read before it resolves.
    http.expectOne('/api/auth/status').flush({ authenticated: true, roles: [] });
    await submitted;

    expect(component.currentPassword()).toBe('');
    expect(component.newPassword()).toBe('');
    expect(component.confirmPassword()).toBe('');
    http.verify();
  });
});
