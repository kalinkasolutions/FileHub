import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { apiErrorMessage } from '@services/api-error';
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

/**
 * Turns one entry into a public link. Two states in one dialog: the limit before the link exists,
 * the link itself afterwards — a link is worth nothing unless it is copied, so the dialog stays
 * open with it on screen instead of closing and leaving the user to find it again.
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
  private readonly toastr = inject(ToastrService);

  public readonly data = inject<IShareDialogData>(MAT_DIALOG_DATA);

  /** 0 is what the API means by unlimited, and what a link gets unless it is capped here. */
  public readonly limit = signal(0);
  public readonly link = signal('');
  public readonly isSaving = signal(false);

  public create(): void {
    this.isSaving.set(true);

    this.shareService
      .create({
        basePathId: this.data.basePathId,
        relativePath: this.data.relativePath,
        maxDownloadCount: this.limit(),
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

  public setLimit(value: string): void {
    const limit = Number(value);
    this.limit.set(Number.isFinite(limit) && limit > 0 ? Math.floor(limit) : 0);
  }
}
