import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { IReceivedShare } from '@models/IReceivedShare';
import { apiErrorMessage } from '@services/api-error';
import { ShareService } from '@services/share.service';
import { ToastrService } from 'ngx-toastr';
import { finalize } from 'rxjs';
import { formatBytes, formatDownloads } from '@util/format';

/**
 * The links aimed at a group this account belongs to — the receiving end of the audience. Without
 * it a member could open a group link somebody had sent them and had no way to find one nobody had:
 * the file may sit under a base path they hold no grant on, so it is in no listing of theirs either.
 *
 * Everything here was aimed by an admin, since nobody else may aim a link at a group, and nothing
 * here is the caller's to revoke — the actions are open and copy, not delete.
 */
@Component({
  standalone: true,
  selector: 'app-received-shares',
  templateUrl: './received-shares.component.html',
  styleUrl: './received-shares.component.scss',
  imports: [DatePipe, MatIconModule],
})
export class ReceivedSharesComponent {
  private readonly shareService = inject(ShareService);
  private readonly toastr = inject(ToastrService);

  public readonly shares = signal<IReceivedShare[]>([]);
  public readonly isLoading = signal(true);

  public readonly count = computed(() => {
    if (this.isLoading()) {
      return '';
    }

    const total = this.shares().length;

    return total === 1 ? '1 link' : `${total} links`;
  });

  public constructor() {
    this.load();
  }

  /** Size is a byte count even for a directory — the API measures a share when it is created. */
  public meta(share: IReceivedShare): string {
    return `${formatBytes(share.size)} · ${formatDownloads(share.downloadCount, share.maxDownloadCount)}`;
  }

  /**
   * The link is an ordinary share URL, so it opens the same landing page an anonymous link does —
   * it just answers nobody outside the group. In a new tab, because leaving the browser to look at
   * one link would lose the directory the user is standing in.
   */
  public open(share: IReceivedShare): void {
    window.open(share.link, '_blank', 'noopener');
  }

  public copy(share: IReceivedShare): void {
    if (!navigator.clipboard) {
      this.toastr.info(share.link);
      return;
    }

    navigator.clipboard
      .writeText(share.link)
      .then(() => this.toastr.success('Link copied to the clipboard'))
      .catch(() => this.toastr.info(share.link));
  }

  private load(): void {
    this.isLoading.set(true);

    this.shareService
      .received()
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: (shares) => this.shares.set(shares),
        error: (error: unknown) =>
          this.toastr.error(apiErrorMessage(error, 'Could not load what was shared with you')),
      });
  }
}
