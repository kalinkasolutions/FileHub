import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { Component, ComponentRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { AuthService } from '@services/auth.service';
import { provideToastr } from 'ngx-toastr';
import { beforeEach, describe, expect, it } from 'vitest';
import { HeaderComponent } from './header.component';

@Component({ standalone: true, template: '' })
class BlankComponent {}

/** Only the three things the header reads off the session. */
class FakeAuthService {
  public readonly authenticated = signal(false);
  public readonly admin = signal(false);

  public readonly isAuthenticated = this.authenticated;
  public readonly isAdmin = this.admin;
}

describe('HeaderComponent', () => {
  let auth: FakeAuthService;
  let header: HeaderComponent;
  let ref: ComponentRef<HeaderComponent>;
  let router: Router;

  beforeEach(() => {
    TestBed.resetTestingModule();
    auth = new FakeAuthService();

    TestBed.configureTestingModule({
      imports: [HeaderComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideToastr(),
        provideRouter([
          { path: '', component: BlankComponent },
          { path: 'account', component: BlankComponent },
          { path: 'admin', component: BlankComponent },
        ]),
        { provide: AuthService, useValue: auth },
      ],
    });

    const fixture = TestBed.createComponent(HeaderComponent);
    header = fixture.componentInstance;
    ref = fixture.componentRef;
    router = TestBed.inject(Router);
  });

  it('shows nothing but the brand without a session', () => {
    expect(header.signedIn()).toBe(false);
    expect(header.isAdmin()).toBe(false);
  });

  it('offers the admin area only to an admin', () => {
    auth.authenticated.set(true);
    expect(header.signedIn()).toBe(true);
    expect(header.isAdmin()).toBe(false);

    auth.admin.set(true);
    expect(header.isAdmin()).toBe(true);
  });

  it('stays in its signed-out form when the route asks for it, session or not', () => {
    auth.authenticated.set(true);
    auth.admin.set(true);
    ref.setInput('anonymous', true);

    expect(header.signedIn()).toBe(false);
    expect(header.isAdmin()).toBe(false);
  });

  it('names the section the current url is in', async () => {
    expect(header.section()).toBe('Files');

    await router.navigateByUrl('/account');
    expect(header.section()).toBe('Account');

    await router.navigateByUrl('/admin');
    expect(header.section()).toBe('Admin');

    await router.navigateByUrl('/');
    expect(header.section()).toBe('Files');
  });
});
