import { DatePipe } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { MatIcon } from '@angular/material/icon';
import { IAdminShare, downloadsLabel, shareLocation } from '@models/IAdminShare';
import { formatBytes } from '@util/format';
import { AdminShareService } from '@services/admin-share.service';
import { apiErrorMessage } from '@services/api-error';
import { ToastrService } from 'ngx-toastr';
import { confirm } from '../confirm/confirm-dialog.component';

/**
 * Every public link in the installation, whoever made it. These are the only URLs FileHub answers
 * without a session, so this list is the one place to see — and revoke — what is reachable from
 * the internet.
 */
@Component({
  selector: 'admin-shares',
  standalone: true,
  imports: [DatePipe, MatIcon],
  templateUrl: 'admin-shares.component.html',
  styleUrl: 'admin-shares.component.scss',
})
export class AdminSharesComponent implements OnInit {
  private readonly shareService = inject(AdminShareService);
  private readonly toastr = inject(ToastrService);
  private readonly dialog = inject(MatDialog);

  public readonly shares = this.shareService.shares;

  public ngOnInit(): void {
    this.shareService.load().subscribe({
      error: (error: unknown) =>
        this.toastr.error(apiErrorMessage(error, 'Could not load the share links')),
    });
  }

  public location(share: IAdminShare): string {
    return shareLocation(share);
  }

  public downloads(share: IAdminShare): string {
    return downloadsLabel(share);
  }

  public size(share: IAdminShare): string {
    return formatBytes(share.size);
  }

  public copy(share: IAdminShare): void {
    navigator.clipboard.writeText(share.link).then(
      () => this.toastr.success('Link copied'),
      // Clipboard access can be refused outright (an insecure origin, a locked-down browser), and
      // then the only useful thing left is to say so.
      () => this.toastr.error('The browser would not let FileHub write to the clipboard'),
    );
  }

  public remove(share: IAdminShare): void {
    confirm(this.dialog, {
      title: `Revoke the link to ${share.name}?`,
      message:
        `The link stops working immediately for everyone who has it. ${share.createdBy} is not ` +
        `told, and the file itself is untouched.`,
      confirm: 'Revoke link',
    }).subscribe((confirmed) => {
      if (!confirmed) {
        return;
      }

      this.shareService.remove(share.id).subscribe({
        next: () => this.toastr.success('Link revoked'),
        error: (error: unknown) =>
          this.toastr.error(apiErrorMessage(error, 'Could not revoke the link')),
      });
    });
  }
}
