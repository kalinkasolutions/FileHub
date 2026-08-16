import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatDialog } from '@angular/material/dialog';
import { MatIcon } from '@angular/material/icon';
import { IBasePath, grantLabel, isUngranted } from '@models/IBasePath';
import { AdminGroupService } from '@services/admin-group.service';
import { AdminUserService } from '@services/admin-user.service';
import { apiErrorMessage } from '@services/api-error';
import { BasePathService } from '@services/basepath.service';
import { ToastrService } from 'ngx-toastr';
import { finalize } from 'rxjs';
import { AccessListComponent, IAccessOption, revokedIds } from '../access/access-list.component';
import { confirm } from '../confirm/confirm-dialog.component';

/** Which of the two grant tables the open editor is editing. */
type AccessKind = 'users' | 'groups';

/**
 * The directories FileHub is allowed to read, and who may see each one.
 *
 * Three things on this screen are the access model rather than decoration. A base path is reached
 * by three routes — a direct grant, a group grant, or the Admin role, which is an implicit grant of
 * every base path — so the row shows both counts and says outright when neither is set, because
 * that state is invisible to everyone *but* an admin rather than invisible to everybody. Deleting
 * one revokes every share link into it. And revoking a grant deletes the links that user made under
 * it, which is why both of those are a confirmation rather than a save.
 */
@Component({
  selector: 'admin-base-paths',
  standalone: true,
  imports: [FormsModule, MatIcon, AccessListComponent],
  templateUrl: 'basepaths.component.html',
  styleUrl: 'basepaths.component.scss',
})
export class BasePathsComponent implements OnInit {
  private readonly basePathService = inject(BasePathService);
  private readonly userService = inject(AdminUserService);
  private readonly groupService = inject(AdminGroupService);
  private readonly toastr = inject(ToastrService);
  private readonly dialog = inject(MatDialog);

  public readonly basePaths = this.basePathService.basePaths;

  public readonly newPath = signal('');
  public readonly newName = signal('');
  public readonly busy = signal(false);

  /** The row being renamed / repointed, or null. Only one row is ever open. */
  public readonly editingId = signal<string | null>(null);
  public readonly editPath = signal('');
  public readonly editName = signal('');

  /** The row whose grants are open, which of its two lists, and the ids as the server reported them. */
  public readonly accessFor = signal<IBasePath | null>(null);
  public readonly accessKind = signal<AccessKind>('users');
  public readonly accessGranted = signal<string[]>([]);

  /** Every account, as the tick list of the user-grant editor. */
  public readonly userOptions = computed<IAccessOption[]>(() =>
    this.userService.users().map((user) => ({
      id: user.id,
      label: user.username,
      hint: user.email,
    })),
  );

  /** Every group, as the tick list of the group-grant editor. */
  public readonly groupOptions = computed<IAccessOption[]>(() =>
    this.groupService.groups().map((group) => ({
      id: group.id,
      label: group.name,
      hint: `${group.memberCount} member(s)`,
    })),
  );

  public readonly accessOptions = computed<IAccessOption[]>(() =>
    this.accessKind() === 'users' ? this.userOptions() : this.groupOptions(),
  );

  public readonly accessHeading = computed(() => {
    const name = this.accessFor()?.name ?? '';
    return this.accessKind() === 'users' ? `Users who may see ${name}` : `Groups granted ${name}`;
  });

  public readonly accessEmptyText = computed(() =>
    this.accessKind() === 'users'
      ? 'There are no accounts yet. Invite one under Users.'
      : 'There are no groups yet. Create one under Groups.',
  );

  public readonly accessNote = computed(() =>
    this.accessKind() === 'users'
      ? 'Unticking an account revokes this base path and deletes the share links it made under ' +
        'it — unless a group still grants it. Admins keep it either way.'
      : 'Unticking a group revokes this base path from every member, and deletes the share links ' +
        'they made under it — unless they still reach it another way.',
  );

  public ngOnInit(): void {
    this.basePathService.load().subscribe({
      error: (error: unknown) =>
        this.toastr.error(apiErrorMessage(error, 'Could not load the base paths')),
    });

    // The grant editors tick accounts and groups, so this screen needs both lists by name as well
    // as by id.
    this.userService.load().subscribe({
      error: (error: unknown) => this.toastr.error(apiErrorMessage(error, 'Could not load users')),
    });

    this.groupService.load().subscribe({
      error: (error: unknown) => this.toastr.error(apiErrorMessage(error, 'Could not load groups')),
    });
  }

  public grants(basePath: IBasePath): string {
    return grantLabel(basePath);
  }

  public ungranted(basePath: IBasePath): boolean {
    return isUngranted(basePath);
  }

  public add(): void {
    this.busy.set(true);
    this.basePathService
      .create({ path: this.newPath().trim(), name: this.newName().trim() })
      .pipe(finalize(() => this.busy.set(false)))
      .subscribe({
        next: (created) => {
          this.newPath.set('');
          this.newName.set('');
          this.toastr.success(`${created.name} added. Only admins can see it until it is granted.`);
        },
        // The API says whether the path was relative, missing or not a directory — that message is
        // the whole answer, so it goes through unedited.
        error: (error: unknown) =>
          this.toastr.error(apiErrorMessage(error, 'Could not add the base path')),
      });
  }

  public startEdit(basePath: IBasePath): void {
    this.accessFor.set(null);
    this.editingId.set(basePath.id);
    this.editPath.set(basePath.path);
    this.editName.set(basePath.name);
  }

  public cancelEdit(): void {
    this.editingId.set(null);
  }

  public saveEdit(basePath: IBasePath): void {
    this.busy.set(true);
    this.basePathService
      .update(basePath.id, { path: this.editPath().trim(), name: this.editName().trim() })
      .pipe(finalize(() => this.busy.set(false)))
      .subscribe({
        next: (updated) => {
          this.editingId.set(null);
          this.toastr.success(`${updated.name} updated`);
        },
        error: (error: unknown) =>
          this.toastr.error(apiErrorMessage(error, 'Could not update the base path')),
      });
  }

  public remove(basePath: IBasePath): void {
    confirm(this.dialog, {
      title: `Delete ${basePath.name}?`,
      message:
        `FileHub will stop reading ${basePath.path}, every grant of it — to users and to groups ` +
        `alike — is dropped, and every share link pointing into it stops working. The files ` +
        `themselves are not touched.`,
      confirm: 'Delete base path',
    }).subscribe((confirmed) => {
      if (!confirmed) {
        return;
      }

      this.basePathService.remove(basePath.id).subscribe({
        next: () => this.toastr.success(`${basePath.name} deleted`),
        error: (error: unknown) =>
          this.toastr.error(apiErrorMessage(error, 'Could not delete the base path')),
      });
    });
  }

  // ─── The two grant lists ──────────────────────────────────────────────────

  public openUsers(basePath: IBasePath): void {
    this.openAccess(basePath, 'users');

    this.basePathService.getUsers(basePath.id).subscribe({
      next: (userIds) => this.accessGranted.set(userIds),
      error: (error: unknown) => this.accessFailed(error, 'Could not load who may see this'),
    });
  }

  public openGroups(basePath: IBasePath): void {
    this.openAccess(basePath, 'groups');

    this.basePathService.getGroups(basePath.id).subscribe({
      next: (groupIds) => this.accessGranted.set(groupIds),
      error: (error: unknown) =>
        this.accessFailed(error, 'Could not load which groups are granted this'),
    });
  }

  public closeAccess(): void {
    this.accessFor.set(null);
  }

  /**
   * Every one of these routes replaces the whole list, so a save that drops an id is a revocation —
   * and a revocation here deletes the share links made under this base path by whoever lost it.
   * That is not something to report in a toast afterwards.
   */
  public saveAccess(ids: string[]): void {
    const basePath = this.accessFor();
    if (!basePath) {
      return;
    }

    const lost = revokedIds(this.accessGranted(), ids);
    if (lost.length === 0) {
      this.commitAccess(basePath, ids);
      return;
    }

    const subject = this.accessKind() === 'users' ? 'account(s)' : 'group(s)';

    confirm(this.dialog, {
      title: `Revoke ${basePath.name} from ${lost.length} ${subject}?`,
      message:
        `They lose ${basePath.name} unless they still reach it another way, and every share link ` +
        `they made under it is deleted. Links made by an admin are never revoked.`,
      confirm: 'Save and revoke',
    }).subscribe((confirmed) => {
      if (!confirmed) {
        return;
      }

      this.commitAccess(basePath, ids);
    });
  }

  private openAccess(basePath: IBasePath, kind: AccessKind): void {
    this.editingId.set(null);
    this.accessFor.set(basePath);
    this.accessKind.set(kind);
    this.accessGranted.set([]);
  }

  private accessFailed(error: unknown, fallback: string): void {
    this.accessFor.set(null);
    this.toastr.error(apiErrorMessage(error, fallback));
  }

  private commitAccess(basePath: IBasePath, ids: string[]): void {
    const kind = this.accessKind();
    const save =
      kind === 'users'
        ? this.basePathService.setUsers(basePath.id, ids)
        : this.basePathService.setGroups(basePath.id, ids);

    this.busy.set(true);
    save.pipe(finalize(() => this.busy.set(false))).subscribe({
      next: () => {
        this.accessFor.set(null);
        this.toastr.success(
          kind === 'users'
            ? `${basePath.name} is granted to ${ids.length} user(s)`
            : `${basePath.name} is granted to ${ids.length} group(s)`,
        );
      },
      error: (error: unknown) =>
        this.toastr.error(apiErrorMessage(error, 'Could not save the access list')),
    });
  }
}
