import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, inject, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { NotificationLevel } from '@models/INotifcation';
import { IShare } from '@models/IShare';
import { FileService } from '@services/file.service';
import { NotificationService } from '@services/notification.service';
import { ShareService } from '@services/share.service';
import { FileSize } from '@util/filesize';

@Component({
  standalone: true,
  selector: 'app-publicshare',
  templateUrl: './publicshare.component.html',
  styleUrl: './publicshare.component.scss',
  imports: [CommonModule],
})
export class PublicShare {
  public share: IShare = {
    Id: '',
    Size: 0,
    Name: '',
    IsDir: false,
  };

  private readonly cdr = inject(ChangeDetectorRef);

  constructor(
    private route: ActivatedRoute,
    private fileService: FileService,
    private shareService: ShareService,
    private notificationService: NotificationService,
  ) {
    this.route.paramMap.subscribe((params) => {
      const id = params.get('id') ?? '';
      this.shareService.validateShare(id).subscribe((share) => {
        this.share = share;
        // Zoneless: an rxjs callback doesn't notify the view on its own.
        this.cdr.markForCheck();
      });
    });
  }

  public download() {
    this.fileService.downloadPublicShare(this.share);
  }

  public get Link() {
    // Every other call is relative now that the API serves the SPA, but this one is copied to the
    // clipboard and pasted elsewhere, so it has to carry the origin.
    return `${location.origin}/public-api/files/download/${this.share.Id}`;
  }

  public copyLink(e: Event) {
    e.preventDefault();
    navigator.clipboard
      .writeText(this.Link)
      .then(() => {
        this.notificationService.notify({
          level: NotificationLevel.success,
          title: 'Direct link',
          message: 'Copied to clipboard',
        });
      })
      .catch((err) => {
        this.notificationService.notify({
          level: NotificationLevel.success,
          title: 'Direct link',
          message: `Failed to copy to clipboard ${err.message}`,
        });
      });
  }

  public getFileSize(size: number): string {
    return FileSize.FileSize(size);
  }
}
