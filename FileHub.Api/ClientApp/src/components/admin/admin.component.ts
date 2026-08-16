import { Component, ElementRef, afterNextRender, signal, viewChild } from '@angular/core';
import { MatIcon } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { BasePathsComponent } from './basepaths/basepaths.component';
import { GroupsComponent } from './groups/groups.component';
import { AdminSharesComponent } from './shares/admin-shares.component';
import { EmailSettingsComponent } from './email/email-settings.component';
import { UsersComponent } from './users/users.component';

export type AdminSection = 'users' | 'groups' | 'basePaths' | 'shares' | 'email';

/**
 * The section last looked at. A module variable rather than storage: it survives the shell being
 * torn down and rebuilt — coming back from the browser lands where you left off — but dies with
 * the page, so a fresh visit starts at Users.
 */
let lastSection: AdminSection = 'users';

/**
 * The admin area: one shell, five sections, no nested routes. The `admin` route is a single
 * component and the sections are views of it, which is what lets the header and the tab bar stay
 * put while switching between them.
 *
 * The tab bar sits under the header rather than at the foot of the screen: with five sections a
 * bottom bar has to wrap, and a wrapped bar pushes the content it belongs to off a phone. The top
 * bar scrolls sideways instead.
 *
 * Everything in here is behind `adminGuard`, and the API checks the `Admin` role on every call it
 * receives besides — a 403 from one of these screens is an answer, not a bug.
 *
 * Each section is `@defer`red: the `admin` route is eager (it is in the initial bundle), and five
 * screens' worth of forms and Material overlays would ride along with it otherwise.
 */
@Component({
  selector: 'admin',
  standalone: true,
  imports: [
    RouterLink,
    MatIcon,
    UsersComponent,
    GroupsComponent,
    BasePathsComponent,
    AdminSharesComponent,
    EmailSettingsComponent,
  ],
  templateUrl: 'admin.component.html',
  styleUrl: 'admin.component.scss',
})
export class AdminComponent {
  public readonly section = signal<AdminSection>(lastSection);

  private readonly bar = viewChild<ElementRef<HTMLElement>>('bar');

  constructor() {
    // The bar scrolls sideways rather than wrapping, so on a phone the remembered section can open
    // with its tab off the right-hand edge — and a bar with a hidden scrollbar gives no hint that
    // there is more of it. Tapping a tab scrolls it into view by itself; only the restored one has
    // to be brought back. `block: 'nearest'` keeps it from scrolling the page vertically as well.
    afterNextRender(() => {
      const active = this.bar()?.nativeElement.querySelector('.tab.active');
      active?.scrollIntoView({ block: 'nearest', inline: 'nearest' });
    });
  }

  public show(section: AdminSection): void {
    lastSection = section;
    this.section.set(section);
  }
}
