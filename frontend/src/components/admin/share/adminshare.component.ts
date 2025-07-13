import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { IAdminShare } from '@models/IAdminShare';
import { NotificationLevel } from '@models/INotifcation';
import { NotificationService } from '@services/notification.service';
import { PathService } from '@services/path.service';
import { ShareService } from '@services/share.service';

@Component({
    standalone: true,
    selector: 'app-admin-share',
    templateUrl: './adminshare.component.html',
    imports: [CommonModule, FormsModule]
})
export class AdminShareComponent implements OnInit {
    public sharedPaths: IAdminShare[] = [];

    constructor(private shareService: ShareService, private notificationService: NotificationService) { }

    public ngOnInit(): void {
        this.shareService.getShares().subscribe(sharedPaths => {
            this.sharedPaths = sharedPaths;
        });
    }

    public getShareName(path: string) {
        return PathService.getPathName(path);
    }

    public copyShareLink(share: IAdminShare) {
        navigator.clipboard.writeText(share.Link);
        this.notificationService.notify({
            level: NotificationLevel.success,
            title: "Share link created",
            message: `Link for '${PathService.getPathName(share.Path)}' bcopied to clipboard`,
        });
    }

    public deleteEntry(share: IAdminShare) {
        this.shareService.delete(share).subscribe(deletedShare => {
            this.sharedPaths = this.sharedPaths.filter(x => x.Id !== deletedShare.Id)
            this.notificationService.notify({
                title: "Share successfully deleted",
                message: `The share ${PathService.getPathName(deletedShare.Path)} was deleted from the shares.`,
                level: NotificationLevel.success,
            });
        })
    }
}
