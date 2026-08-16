import { Component, signal } from '@angular/core';
import { MatIcon } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { BasePathsComponent } from './basepaths/basepaths.component';
import { AdminSharesComponent } from './shares/admin-shares.component';
import { EmailSettingsComponent } from './email/email-settings.component';
import { UsersComponent } from './users/users.component';

export type AdminSection = 'users' | 'basePaths' | 'shares' | 'email';

/**
 * The section last looked at. A module variable rather than storage: it survives the shell being
 * torn down and rebuilt — coming back from the browser lands where you left off — but dies with
 * the page, so a fresh visit starts at Users.
 */
let lastSection: AdminSection = 'users';

/**
 * The admin area: one shell, four sections, no nested routes. The `admin` route is a single
 * component and the sections are views of it, which is what lets the header and the tab bar stay
 * put while switching between them.
 *
 * Everything in here is behind `adminGuard`, and the API checks the `Admin` role on every call it
 * receives besides — a 403 from one of these screens is an answer, not a bug.
 *
 * Each section is `@defer`red: the `admin` route is eager (it is in the initial bundle), and four
 * screens' worth of forms and Material overlays would ride along with it otherwise.
 */
@Component({
  selector: 'admin',
  standalone: true,
  imports: [
    RouterLink,
    MatIcon,
    UsersComponent,
    BasePathsComponent,
    AdminSharesComponent,
    EmailSettingsComponent,
  ],
  templateUrl: 'admin.component.html',
  styleUrl: 'admin.component.scss',
})
export class AdminComponent {
  public readonly section = signal<AdminSection>(lastSection);

  public show(section: AdminSection): void {
    lastSection = section;
    this.section.set(section);
  }
}
