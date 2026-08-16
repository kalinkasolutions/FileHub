import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { IShareLink } from '@models/IShareLink';
import { apiErrorMessage } from '@services/api-error';
import { ShareService } from '@services/share.service';
import { ToastrService } from 'ngx-toastr';
import { finalize } from 'rxjs';
import { formatBytes, formatDownloads } from './format';

/**
 * The links this user has handed out, and the only way to take one back: the admin share list spans
 * every user and is behind the admin role, so without this an ordinary account can create links it
 * can never see again.
 */
@Component({
  standalone: true,
  selector: 'app-share-links',
  templateUrl: './share-links.component.html',
  styleUrl: './share-links.component.scss',
  imports: [DatePipe, MatIconModule],
})
export class ShareLinksComponent {
  private readonly shareService = inject(ShareService);
  private readonly toastr = inject(ToastrService);

  public readonly shares = signal<IShareLink[]>([]);
  public readonly isLoading = signal(true);

  public constructor() {
    this.load();
  }

  /** Size is a byte count here even for a directory — the API measures a share when it is created,
   * unlike a listing row, where a directory's size is how many entries it holds. */
  public meta(share: IShareLink): string {
    return `${formatBytes(share.size)} · ${formatDownloads(share.downloadCount, share.maxDownloadCount)}`;
  }

  public copy(share: IShareLink): void {
    if (!navigator.clipboard) {
      this.toastr.info(share.link);
      return;
    }

    navigator.clipboard
      .writeText(share.link)
      .then(() => this.toastr.success('Link copied to the clipboard'))
      .catch(() => this.toastr.info(share.link));
  }

  /** Revoking is immediate and final: the link stops working, and there is nothing to undo it with. */
  public revoke(share: IShareLink): void {
    this.shareService.revoke(share.id).subscribe({
      next: () => {
        this.shares.update((shares) => shares.filter((x) => x.id !== share.id));
        this.toastr.success(`The link to ${share.name} was revoked`);
      },
      error: (error: unknown) =>
        this.toastr.error(apiErrorMessage(error, 'Could not revoke that link')),
    });
  }

  private load(): void {
    this.isLoading.set(true);

    this.shareService
      .list()
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: (shares) => this.shares.set(shares),
        error: (error: unknown) =>
          this.toastr.error(apiErrorMessage(error, 'Could not load your links')),
      });
  }
}
