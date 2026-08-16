import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatDialog } from '@angular/material/dialog';
import { MatIcon } from '@angular/material/icon';
import { IBasePath } from '@models/IBasePath';
import { AdminUserService } from '@services/admin-user.service';
import { apiErrorMessage } from '@services/api-error';
import { BasePathService } from '@services/basepath.service';
import { ToastrService } from 'ngx-toastr';
import { finalize } from 'rxjs';
import { AccessListComponent, IAccessOption } from '../access/access-list.component';
import { confirm } from '../confirm/confirm-dialog.component';

/**
 * The directories FileHub is allowed to read, and who may see each one.
 *
 * Two things about this screen are the access model rather than decoration: a base path with no
 * users granted is invisible to everybody including admins, which is why the row says so; and
 * deleting one revokes every share link into it, which is why the confirmation says that instead
 * of "are you sure".
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

  /** The row whose grants are open, and the ids as the server last reported them. */
  public readonly accessFor = signal<IBasePath | null>(null);
  public readonly accessGranted = signal<string[]>([]);

  /** Every account, as the tick list of the grant editor. */
  public readonly userOptions = computed<IAccessOption[]>(() =>
    this.userService.users().map((user) => ({
      id: user.id,
      label: user.username,
      hint: user.email,
    })),
  );

  public ngOnInit(): void {
    this.basePathService.load().subscribe({
      error: (error: unknown) =>
        this.toastr.error(apiErrorMessage(error, 'Could not load the base paths')),
    });

    // The grant editor ticks users, so this screen needs their names as well as their ids.
    this.userService.load().subscribe({
      error: (error: unknown) => this.toastr.error(apiErrorMessage(error, 'Could not load users')),
    });
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
          this.toastr.success(`${created.name} added. Grant it to someone to make it visible.`);
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
        `FileHub will stop reading ${basePath.path}, and every share link pointing into it stops ` +
        `working. The files themselves are not touched.`,
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

  public openAccess(basePath: IBasePath): void {
    this.editingId.set(null);
    this.accessFor.set(basePath);
    this.accessGranted.set([]);

    this.basePathService.getUsers(basePath.id).subscribe({
      next: (userIds) => this.accessGranted.set(userIds),
      error: (error: unknown) => {
        this.accessFor.set(null);
        this.toastr.error(apiErrorMessage(error, 'Could not load who may see this base path'));
      },
    });
  }

  public closeAccess(): void {
    this.accessFor.set(null);
  }

  public saveAccess(userIds: string[]): void {
    const basePath = this.accessFor();
    if (!basePath) {
      return;
    }

    this.busy.set(true);
    this.basePathService
      .setUsers(basePath.id, userIds)
      .pipe(finalize(() => this.busy.set(false)))
      .subscribe({
        next: () => {
          this.accessFor.set(null);
          this.toastr.success(
            userIds.length === 0
              ? `${basePath.name} is no longer visible to anyone`
              : `${basePath.name} is visible to ${userIds.length} user(s)`,
          );
        },
        error: (error: unknown) =>
          this.toastr.error(apiErrorMessage(error, 'Could not save the access list')),
      });
  }
}
