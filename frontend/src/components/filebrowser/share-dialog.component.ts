import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { IGroupSummary } from '@models/IGroupSummary';
import { apiErrorMessage } from '@services/api-error';
import { AuthService } from '@services/auth.service';
import { GroupService } from '@services/group.service';
import { ShareService } from '@services/share.service';
import { ToastrService } from 'ngx-toastr';
import { finalize } from 'rxjs';

/** What the browser hands the dialog: the row that is being shared, addressed as the API wants it. */
export interface IShareDialogData {
  name: string;
  basePathId: string;
  /** Path below the base path, without a leading separator. */
  relativePath: string;
}

/** The audience picker's "no group" option. An empty string, because that is what a `<select>` holds. */
const anyone = '';

/**
 * Turns one entry into a link. Two states in one dialog: the audience and the limit before the link
 * exists, the link itself afterwards — a link is worth nothing unless it is copied, so the dialog
 * stays open with it on screen instead of closing and leaving the user to find it again.
 *
 * The audience is the one choice that changes what the URL is worth: left alone the link is
 * anonymous, and aimed at a group it only answers signed-in members of it. Because that is invisible
 * in the URL itself, the dialog says which of the two it made, in both states.
 *
 * **The picker is admin-only**, and so is the API it posts to. A group link is read by members who
 * may hold no route to the base path, which makes aiming one an access decision rather than a
 * narrower way to publish. For everybody else this dialog is what it was before groups existed: a
 * limit, and an anonymous URL.
 */
@Component({
  standalone: true,
  selector: 'app-share-dialog',
  templateUrl: './share-dialog.component.html',
  styleUrl: './share-dialog.component.scss',
  imports: [FormsModule, MatDialogModule, MatIconModule],
})
export class ShareDialogComponent {
  private readonly shareService = inject(ShareService);
  private readonly groupService = inject(GroupService);
  private readonly authService = inject(AuthService);
  private readonly toastr = inject(ToastrService);

  /** Only an admin may aim a link at a group, so only an admin is offered the choice. */
  public readonly canAim = this.authService.isAdmin;

  public readonly data = inject<IShareDialogData>(MAT_DIALOG_DATA);

  /** 0 is what the API means by unlimited, and what a link gets unless it is capped here. */
  public readonly limit = signal(0);
  public readonly link = signal('');
  public readonly isSaving = signal(false);

  /**
   * Empty until the groups arrive, and empty for good for anyone who is not an admin — the picker is
   * hidden in that case rather than shown with one option, which would only raise a question the
   * user cannot act on.
   */
  public readonly groups = signal<IGroupSummary[]>([]);
  public readonly audienceGroupId = signal(anyone);

  /** The group the link is aimed at, or null while it is still anonymous-by-URL. */
  public readonly audience = computed<IGroupSummary | null>(() => {
    const id = this.audienceGroupId();

    return this.groups().find((group) => group.id === id) ?? null;
  });

  public constructor() {
    // Not even asked for unless the answer can be acted on: for an admin the route lists every
    // group, and for everyone else the picker is not drawn, so the call would be spent on nothing.
    if (!this.canAim()) {
      return;
    }

    this.groupService.list().subscribe({
      // An install with no groups is the ordinary case and not a failure; so is the request failing.
      // Either way the dialog keeps working exactly as it did before groups existed.
      next: (groups) => this.groups.set(groups),
      error: () => this.groups.set([]),
    });
  }

  public create(): void {
    this.isSaving.set(true);

    this.shareService
      .create({
        basePathId: this.data.basePathId,
        relativePath: this.data.relativePath,
        maxDownloadCount: this.limit(),
        audienceGroupId: this.audienceGroupId() === anyone ? null : this.audienceGroupId(),
      })
      .pipe(finalize(() => this.isSaving.set(false)))
      .subscribe({
        next: (share) => {
          this.link.set(share.link);
          this.copy();
        },
        error: (error: unknown) =>
          this.toastr.error(apiErrorMessage(error, 'Could not create that link')),
      });
  }

  public copy(): void {
    // Absent over plain http, which is a normal way to reach a home server — the link is on screen
    // either way, so say so rather than failing silently.
    if (!navigator.clipboard) {
      this.toastr.info('Copy the link from the box above');
      return;
    }

    navigator.clipboard
      .writeText(this.link())
      .then(() => this.toastr.success('Link copied to the clipboard'))
      .catch(() => this.toastr.info('Copy the link from the box above'));
  }

  public setAudience(groupId: string): void {
    this.audienceGroupId.set(groupId);
  }

  public setLimit(value: string): void {
    const limit = Number(value);
    this.limit.set(Number.isFinite(limit) && limit > 0 ? Math.floor(limit) : 0);
  }
}
