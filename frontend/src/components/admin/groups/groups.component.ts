import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatDialog } from '@angular/material/dialog';
import { MatIcon } from '@angular/material/icon';
import {
  IGroup,
  basePathCountLabel,
  groupLossLabel,
  groupWarning,
  memberCountLabel,
} from '@models/IGroup';
import { AdminGroupService } from '@services/admin-group.service';
import { AdminUserService } from '@services/admin-user.service';
import { apiErrorMessage } from '@services/api-error';
import { BasePathService } from '@services/basepath.service';
import { ToastrService } from 'ngx-toastr';
import { finalize } from 'rxjs';
import { AccessListComponent, IAccessOption, revokedIds } from '../access/access-list.component';
import { confirm } from '../confirm/confirm-dialog.component';

/** Which of a group's two lists the open editor is editing. */
type AccessKind = 'members' | 'basePaths';

/**
 * Groups: a named set of accounts that base paths are granted to, and the second of the three
 * routes to a directory (the others being a direct grant and the Admin role, which reaches every
 * base path without a grant). A user's access is the *union* of all of them, so a group grants and
 * never takes away.
 *
 * A group is only its name plus two lists, and both lists are replaced wholesale by their route —
 * so both editors here save a complete set and both ask before a save that drops something. That is
 * not politeness: losing a membership, or a base path off the group, deletes the share links the
 * affected members made under those paths, and deleting the group deletes the links aimed *at* it
 * as their audience.
 */
@Component({
  selector: 'admin-groups',
  standalone: true,
  imports: [FormsModule, MatIcon, AccessListComponent],
  templateUrl: 'groups.component.html',
  styleUrl: 'groups.component.scss',
})
export class GroupsComponent implements OnInit {
  private readonly groupService = inject(AdminGroupService);
  private readonly userService = inject(AdminUserService);
  private readonly basePathService = inject(BasePathService);
  private readonly toastr = inject(ToastrService);
  private readonly dialog = inject(MatDialog);

  public readonly groups = this.groupService.groups;

  public readonly newName = signal('');
  public readonly busy = signal(false);

  /** Gated on by the empty message — see {@link AdminGroupService.loaded}. */
  public readonly loaded = this.groupService.loaded;

  /** The row being renamed, or null. Only one row is ever open. */
  public readonly editingId = signal<string | null>(null);
  public readonly editName = signal('');

  /** The row whose list is open, which of the two, and the ids as the server reported them. */
  public readonly accessFor = signal<IGroup | null>(null);
  public readonly accessKind = signal<AccessKind>('members');
  public readonly accessGranted = signal<string[]>([]);

  public readonly userOptions = computed<IAccessOption[]>(() =>
    this.userService.users().map((user) => ({
      id: user.id,
      label: user.username,
      hint: user.email,
    })),
  );

  public readonly basePathOptions = computed<IAccessOption[]>(() =>
    this.basePathService.basePaths().map((basePath) => ({
      id: basePath.id,
      label: basePath.name,
      hint: basePath.path,
    })),
  );

  public readonly accessOptions = computed<IAccessOption[]>(() =>
    this.accessKind() === 'members' ? this.userOptions() : this.basePathOptions(),
  );

  /** Whichever of the two lists the open editor is ticking has been read at least once. */
  public readonly accessOptionsLoaded = computed(() =>
    this.accessKind() === 'members' ? this.userService.loaded() : this.basePathService.loaded(),
  );

  public readonly accessHeading = computed(() => {
    const name = this.accessFor()?.name ?? '';
    return this.accessKind() === 'members' ? `Members of ${name}` : `Base paths ${name} grants`;
  });

  public readonly accessEmptyText = computed(() =>
    this.accessKind() === 'members'
      ? 'There are no accounts yet. Invite one under Users.'
      : 'There are no base paths yet. Add one under Paths.',
  );

  public readonly accessNote = computed(() =>
    this.accessKind() === 'members'
      ? 'Unticking an account removes it from the group and takes the base paths the group ' +
        'grants — and the share links it made under them — unless it reaches them another way.'
      : 'Unticking a base path takes it from every member of this group, along with the share ' +
        'links they made under it, unless they reach it another way.',
  );

  public ngOnInit(): void {
    this.groupService.load().subscribe({
      error: (error: unknown) => this.toastr.error(apiErrorMessage(error, 'Could not load groups')),
    });

    // The two editors tick accounts and base paths, so this screen needs both by name.
    this.userService.load().subscribe({
      error: (error: unknown) => this.toastr.error(apiErrorMessage(error, 'Could not load users')),
    });

    this.basePathService.load().subscribe({
      error: (error: unknown) =>
        this.toastr.error(apiErrorMessage(error, 'Could not load the base paths')),
    });
  }

  public members(group: IGroup): string {
    return memberCountLabel(group);
  }

  public basePaths(group: IGroup): string {
    return basePathCountLabel(group);
  }

  public warning(group: IGroup): string {
    return groupWarning(group);
  }

  public create(): void {
    this.busy.set(true);
    this.groupService
      .create({ name: this.newName().trim() })
      .pipe(finalize(() => this.busy.set(false)))
      .subscribe({
        next: (created) => {
          this.newName.set('');
          this.toastr.success(`${created.name} created. Add members and base paths to it.`);
        },
        // A duplicate name is a plain 400 with the name in it — that message is the whole answer.
        error: (error: unknown) =>
          this.toastr.error(apiErrorMessage(error, 'Could not create the group')),
      });
  }

  public startEdit(group: IGroup): void {
    this.accessFor.set(null);
    this.editingId.set(group.id);
    this.editName.set(group.name);
  }

  public cancelEdit(): void {
    this.editingId.set(null);
  }

  public saveEdit(group: IGroup): void {
    this.busy.set(true);
    this.groupService
      .rename(group.id, { name: this.editName().trim() })
      .pipe(finalize(() => this.busy.set(false)))
      .subscribe({
        next: (updated) => {
          this.editingId.set(null);
          this.toastr.success(`Renamed to ${updated.name}`);
        },
        error: (error: unknown) =>
          this.toastr.error(apiErrorMessage(error, 'Could not rename the group')),
      });
  }

  public remove(group: IGroup): void {
    confirm(this.dialog, {
      title: `Delete ${group.name}?`,
      message:
        `${memberCountLabel(group)} in it. ${groupLossLabel(group)} Every share link aimed at ` +
        `${group.name} as its audience stops working too. The accounts themselves are not touched.`,
      confirm: 'Delete group',
    }).subscribe((confirmed) => {
      if (!confirmed) {
        return;
      }

      this.groupService.remove(group.id).subscribe({
        next: () => this.toastr.success(`${group.name} deleted`),
        error: (error: unknown) =>
          this.toastr.error(apiErrorMessage(error, 'Could not delete the group')),
      });
    });
  }

  // ─── The two lists ────────────────────────────────────────────────────────

  public openMembers(group: IGroup): void {
    this.openAccess(group, 'members');

    this.groupService.getMembers(group.id).subscribe({
      next: (userIds) => this.accessGranted.set(userIds),
      error: (error: unknown) => this.accessFailed(error, 'Could not load the members'),
    });
  }

  public openBasePaths(group: IGroup): void {
    this.openAccess(group, 'basePaths');

    this.groupService.getBasePaths(group.id).subscribe({
      next: (basePathIds) => this.accessGranted.set(basePathIds),
      error: (error: unknown) => this.accessFailed(error, 'Could not load what this group grants'),
    });
  }

  public closeAccess(): void {
    this.accessFor.set(null);
  }

  public saveAccess(ids: string[]): void {
    const group = this.accessFor();
    if (!group) {
      return;
    }

    const lost = revokedIds(this.accessGranted(), ids);
    if (lost.length === 0) {
      this.commitAccess(group, ids);
      return;
    }

    if (this.accessKind() === 'members') {
      this.confirmRevoke(
        group,
        ids,
        `Remove ${lost.length} account(s) from ${group.name}?`,
        groupLossLabel(group),
      );
      return;
    }

    this.confirmRevoke(
      group,
      ids,
      `Take ${lost.length} base path(s) off ${group.name}?`,
      group.memberCount === 0
        ? 'Nobody is in this group, so nothing anyone can read changes today.'
        : `Its ${memberCountLabel(group)} lose them, unless they reach them another way, and ` +
            `every share link they made under them is deleted.`,
    );
  }

  private openAccess(group: IGroup, kind: AccessKind): void {
    this.editingId.set(null);
    this.accessFor.set(group);
    this.accessKind.set(kind);
    this.accessGranted.set([]);
  }

  private accessFailed(error: unknown, fallback: string): void {
    this.accessFor.set(null);
    this.toastr.error(apiErrorMessage(error, fallback));
  }

  private confirmRevoke(group: IGroup, ids: string[], title: string, message: string): void {
    confirm(this.dialog, { title, message, confirm: 'Save and revoke' }).subscribe((confirmed) => {
      if (!confirmed) {
        return;
      }

      this.commitAccess(group, ids);
    });
  }

  private commitAccess(group: IGroup, ids: string[]): void {
    const kind = this.accessKind();
    const save =
      kind === 'members'
        ? this.groupService.setMembers(group.id, ids)
        : this.groupService.setBasePaths(group.id, ids);

    this.busy.set(true);
    save.pipe(finalize(() => this.busy.set(false))).subscribe({
      next: () => {
        this.accessFor.set(null);
        this.toastr.success(
          kind === 'members'
            ? `${group.name} now has ${ids.length} member(s)`
            : `${group.name} now grants ${ids.length} base path(s)`,
        );
      },
      error: (error: unknown) => this.toastr.error(apiErrorMessage(error, 'Could not save it')),
    });
  }
}
