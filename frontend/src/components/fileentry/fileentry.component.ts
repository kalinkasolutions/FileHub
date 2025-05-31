import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { IPublicPath } from '@models/IPublicPath';

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

    public entryClicked(entry: IPublicPath) {
        if (entry.IsDir) {
            this.navigateToDirectory.emit(entry);
        }
    }

    public getFileSize(entry: IPublicPath): string {
        if (entry.IsDir) {
            return `${entry.Size} items`;
        }
        const gigabytes = Number((entry.Size / 1000 / 1000 / 1000).toFixed(2));
        if (gigabytes >= 1) {
            75
            return `${gigabytes} Gb`;
        }
        const megabytes = Number((entry.Size / 1000 / 1000).toFixed(2));
        if (megabytes >= 1) {
            return `${megabytes} Mb`;
        }
        const kilobytes = Number((entry.Size / 1000).toFixed(2));
        if (kilobytes >= 1) {
            return `${kilobytes} Kb`;
        }
        if (entry.Size >= 1) {
            return `${entry.Size} bytes`
        }
        return "";
    }
}
