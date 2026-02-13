import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { environment } from '@env/environment';
import { NotificationLevel } from '@models/INotifcation';
import { IShare } from '@models/IShare';
import { FileService } from '@services/file.service';
import { NotificationService } from '@services/notification.service';
import { ShareService } from '@services/share.service';
import { FileSize } from 'util/filesize';

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
      });
    });
  }

  public download() {
    this.fileService.downloadPublicShare(this.share);
  }

  public get Link() {
    return `${environment.apiUrl}/public-api/files/download/${this.share.Id}`;
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
