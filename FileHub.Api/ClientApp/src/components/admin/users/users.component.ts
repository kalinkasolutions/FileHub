import { Component, OnInit, computed, inject, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatDialog } from '@angular/material/dialog';
import { MatIcon } from '@angular/material/icon';
import { MatMenu, MatMenuItem, MatMenuTrigger } from '@angular/material/menu';
import {
  IAdminUser,
  accessLabel,
  toggleRole,
  userStatus,
  userStatusLabel,
} from '@models/IAdminUser';
import { Roles } from '@models/roles';
import { AdminUserService } from '@services/admin-user.service';
import { apiErrorMessage } from '@services/api-error';
import { AuthService } from '@services/auth.service';
import { BasePathService } from '@services/basepath.service';
import { RoleService } from '@services/role.service';
import { ToastrService } from 'ngx-toastr';
import { finalize } from 'rxjs';
import { AccessListComponent, IAccessOption, revokedIds } from '../access/access-list.component';
import { confirm } from '../confirm/confirm-dialog.component';

/**
 * Accounts. This screen *is* registration: FileHub has no sign-up page, so an account exists only
 * because an admin invited one from here, and the invitation arrives by mail — which is why a
 * failed send is reported as loudly as it is, and why the screen points at the email settings.
 *
 * The API refuses to delete, disable or demote the last usable admin and refuses an email change
 * here. Those messages explain themselves, so they are shown as they arrive rather than replaced.
 */
@Component({
  selector: 'admin-users',
  standalone: true,
  imports: [FormsModule, MatIcon, MatMenu, MatMenuItem, MatMenuTrigger, AccessListComponent],
  templateUrl: 'users.component.html',
  styleUrl: 'users.component.scss',
})
export class UsersComponent implements OnInit {
  private readonly userService = inject(AdminUserService);
  private readonly roleService = inject(RoleService);
  private readonly basePathService = inject(BasePathService);
  private readonly authService = inject(AuthService);
  private readonly toastr = inject(ToastrService);
  private readonly dialog = inject(MatDialog);

  /** Emitted when the admin follows the "set up email" hint — the shell switches section. */
  public readonly showEmail = output<void>();
  /** Same idea for the pointer at group membership, which is not editable from this screen. */
  public readonly showGroups = output<void>();

  public readonly users = this.userService.users;
  public readonly roles = this.roleService.roles;
  public readonly userRole = Roles.User;
  public readonly adminRole = Roles.Admin;

  /** Revoking a direct grant deletes the links made under it, so the editor says so before saving. */
  public readonly accessNote =
    'Unticking a base path revokes it and deletes the share links this account made under it — ' +
    'unless a group still grants it, or the account is an admin.';

  public readonly signedInAs = computed(() => this.authService.status()?.userId ?? '');

  public readonly busy = signal(false);

  public readonly inviteOpen = signal(false);
  public readonly inviteUsername = signal('');
  public readonly inviteEmail = signal('');
  public readonly inviteRoles = signal<string[]>([Roles.User]);

  /**
   * An account that was created but whose invitation mail did not go out. It stays on screen until
   * dismissed: a toast would scroll away, and this is the one failure that leaves an account
   * nobody can sign in to.
   */
  public readonly undeliveredInvite = signal<IAdminUser | null>(null);
  /** The SMTP error behind it, so the admin is not sent to the email screen to find out why. */
  public readonly undeliveredInviteReason = signal('');

  public readonly editingId = signal<string | null>(null);
  public readonly editUsername = signal('');
  public readonly editRoles = signal<string[]>([]);

  public readonly accessFor = signal<IAdminUser | null>(null);
  public readonly accessGranted = signal<string[]>([]);

  /** Every base path, as the tick list of the grant editor. */
  public readonly basePathOptions = computed<IAccessOption[]>(() =>
    this.basePathService.basePaths().map((basePath) => ({
      id: basePath.id,
      label: basePath.name,
      hint: basePath.path,
    })),
  );

  public ngOnInit(): void {
    this.userService.load().subscribe({
      error: (error: unknown) => this.toastr.error(apiErrorMessage(error, 'Could not load users')),
    });

    this.roleService.load().subscribe({
      error: (error: unknown) => this.toastr.error(apiErrorMessage(error, 'Could not load roles')),
    });

    // The per-user grant editor ticks base paths, so their names are needed here too.
    this.basePathService.load().subscribe({
      error: (error: unknown) =>
        this.toastr.error(apiErrorMessage(error, 'Could not load the base paths')),
    });
  }

  public status(user: IAdminUser): string {
    return userStatus(user);
  }

  public statusLabel(user: IAdminUser): string {
    return userStatusLabel[userStatus(user)];
  }

  /**
   * What this account can reach. The row knows only the *direct* grants — access is their union
   * with every group's, and an admin reaches everything — so the label says which of the three it
   * is talking about rather than claiming a total it cannot compute.
   */
  public access(user: IAdminUser): string {
    return accessLabel(user, Roles.Admin);
  }

  public isSelf(user: IAdminUser): boolean {
    return user.id === this.signedInAs();
  }

  // ─── Inviting ─────────────────────────────────────────────────────────────

  public toggleInvite(): void {
    this.inviteOpen.update((open) => !open);
  }

  public toggleInviteRole(role: string, checked: boolean): void {
    this.inviteRoles.update((roles) => toggleRole(roles, role, checked));
  }

  public hasInviteRole(role: string): boolean {
    return this.inviteRoles().includes(role);
  }

  public invite(): void {
    this.busy.set(true);
    this.userService
      .invite({
        username: this.inviteUsername().trim(),
        email: this.inviteEmail().trim(),
        roles: this.inviteRoles(),
      })
      .pipe(finalize(() => this.busy.set(false)))
      .subscribe({
        next: (result) =>
          this.invited(result.userId, result.inviteMailSent, result.inviteMailError),
        error: (error: unknown) =>
          this.toastr.error(apiErrorMessage(error, 'Could not invite that account')),
      });
  }

  public resendInvite(user: IAdminUser): void {
    this.userService.resendInvite(user.id).subscribe({
      next: () => {
        this.undeliveredInvite.set(null);
        this.undeliveredInviteReason.set('');
        this.toastr.success(`Invitation re-sent to ${user.email}`);
      },
      error: (error: unknown) =>
        this.toastr.error(apiErrorMessage(error, 'Could not send the invitation')),
    });
  }

  public dismissUndelivered(): void {
    this.undeliveredInvite.set(null);
    this.undeliveredInviteReason.set('');
  }

  // ─── Editing ──────────────────────────────────────────────────────────────

  public startEdit(user: IAdminUser): void {
    this.accessFor.set(null);
    this.editingId.set(user.id);
    this.editUsername.set(user.username);
    this.editRoles.set([...user.roles]);
  }

  public cancelEdit(): void {
    this.editingId.set(null);
  }

  public toggleEditRole(role: string, checked: boolean): void {
    this.editRoles.update((roles) => toggleRole(roles, role, checked));
  }

  public hasEditRole(role: string): boolean {
    return this.editRoles().includes(role);
  }

  public saveEdit(user: IAdminUser): void {
    this.busy.set(true);
    this.userService
      // The address is sent back unchanged on purpose: the API rejects a change here, because a new
      // address has to be confirmed from the user's own account screen.
      .update(user.id, {
        username: this.editUsername().trim(),
        email: user.email,
        roles: this.editRoles(),
      })
      .pipe(finalize(() => this.busy.set(false)))
      .subscribe({
        next: () => {
          this.editingId.set(null);
          this.toastr.success('Account updated');
        },
        error: (error: unknown) =>
          this.toastr.error(apiErrorMessage(error, 'Could not update the account')),
      });
  }

  // ─── Lockout and deletion ─────────────────────────────────────────────────

  public setLockout(user: IAdminUser, locked: boolean): void {
    this.userService.setLockout(user.id, locked).subscribe({
      next: () => this.toastr.success(locked ? 'Account disabled' : 'Account enabled'),
      error: (error: unknown) =>
        this.toastr.error(
          apiErrorMessage(error, locked ? 'Could not disable it' : 'Could not enable it'),
        ),
    });
  }

  public remove(user: IAdminUser): void {
    confirm(this.dialog, {
      title: `Delete ${user.username}?`,
      message:
        `The account, its grants, its group memberships and every share link it created are ` +
        `removed. There is no sign-up, so getting ${user.email} back in means inviting it again.`,
      confirm: 'Delete account',
    }).subscribe((confirmed) => {
      if (!confirmed) {
        return;
      }

      this.userService.remove(user.id).subscribe({
        next: () => this.toastr.success(`${user.username} deleted`),
        error: (error: unknown) =>
          this.toastr.error(apiErrorMessage(error, 'Could not delete the account')),
      });
    });
  }

  // ─── Base-path grants, from the user's side ───────────────────────────────

  public openAccess(user: IAdminUser): void {
    this.editingId.set(null);
    this.accessFor.set(user);
    this.accessGranted.set([]);

    this.basePathService.getBasePathsOfUser(user.id).subscribe({
      next: (basePathIds) => this.accessGranted.set(basePathIds),
      error: (error: unknown) => {
        this.accessFor.set(null);
        this.toastr.error(apiErrorMessage(error, 'Could not load what this account may see'));
      },
    });
  }

  public closeAccess(): void {
    this.accessFor.set(null);
  }

  /**
   * The route replaces the whole list, so a save that drops an id is a revocation — and a
   * revocation deletes the share links this account made under the base path it lost. That is
   * worth asking about rather than reporting in a toast afterwards.
   */
  public saveAccess(basePathIds: string[]): void {
    const user = this.accessFor();
    if (!user) {
      return;
    }

    const lost = revokedIds(this.accessGranted(), basePathIds);
    if (lost.length === 0) {
      this.commitAccess(user, basePathIds);
      return;
    }

    confirm(this.dialog, {
      title: `Revoke ${lost.length} base path(s) from ${user.username}?`,
      message:
        `${user.username} loses them unless a group still grants them, and every share link it ` +
        `made under them is deleted. An admin keeps them either way.`,
      confirm: 'Save and revoke',
    }).subscribe((confirmed) => {
      if (!confirmed) {
        return;
      }

      this.commitAccess(user, basePathIds);
    });
  }

  private commitAccess(user: IAdminUser, basePathIds: string[]): void {
    this.busy.set(true);
    this.basePathService
      .setBasePathsOfUser(user.id, basePathIds)
      .pipe(finalize(() => this.busy.set(false)))
      .subscribe({
        next: () => {
          this.accessFor.set(null);
          this.toastr.success(
            basePathIds.length === 0
              ? `${user.username} has no base path granted directly`
              : `${user.username} is granted ${basePathIds.length} base path(s) directly`,
          );
        },
        error: (error: unknown) =>
          this.toastr.error(apiErrorMessage(error, 'Could not save the access list')),
      });
  }

  /**
   * The account exists either way. When the mail did not go out the admin is the only one who can
   * tell the user, so the row is pulled out of the list and kept on screen with the resend action.
   */
  private invited(userId: string, mailSent: boolean, mailError: string): void {
    this.inviteUsername.set('');
    this.inviteEmail.set('');
    this.inviteRoles.set([Roles.User]);
    this.inviteOpen.set(false);

    this.userService.load().subscribe({
      next: (users) => {
        if (mailSent) {
          this.toastr.success('Invitation sent');
          return;
        }

        this.undeliveredInviteReason.set(mailError);
        this.undeliveredInvite.set(users.find((user) => user.id === userId) ?? null);
        this.toastr.warning('The account was created, but the invitation email could not be sent');
      },
      error: (error: unknown) => this.toastr.error(apiErrorMessage(error, 'Could not load users')),
    });
  }
}
