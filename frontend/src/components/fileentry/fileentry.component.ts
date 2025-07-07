import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { NotificationLevel } from '@models/INotifcation';
import { IPublicPath } from '@models/IPublicPath';
import { FileService } from '@services/file.service';
import { NotificationService } from '@services/notification.service';
import { PathService } from '@services/path.service';
import { ShareService } from '@services/share.service';
import { FileSize } from 'util/filesize';

@Component({
    standalone: true,
    selector: 'file-entry',
    templateUrl: './fileentry.component.html',
    styleUrl: './fileentry.component.scss',
    imports: [CommonModule]
})
export class FileEntry {

    @Input() fileEntry: IPublicPath = { Id: 0, Name: '', IsDir: false, Size: 0, NextSegment: "", IsBasePath: false, ItemId: "" };
    @Output() navigateToDirectory = new EventEmitter<IPublicPath>();

    constructor(private fileService: FileService, private shareService: ShareService, private notificationService: NotificationService) { }

    public entryClicked(entry: IPublicPath) {
        if (entry.IsDir) {
            this.navigateToDirectory.emit(entry);
        }
    }

    public download(entry: IPublicPath) {
        this.fileService.download(entry);
    }

    public share(entry: IPublicPath) {
        this.shareService.share(entry).subscribe(share => {
            navigator.clipboard.writeText(share.Link)
            this.notificationService.notify({
                level: NotificationLevel.success,
                title: "Item shared",
                message: "Link copied to clipboard",
            })
        });
    }

    public getFileSize(entry: IPublicPath): string {
        if (entry.IsDir) {
            return `${entry.Size} items`;
        }

        return FileSize.FileSize(entry.Size)

    }
}
