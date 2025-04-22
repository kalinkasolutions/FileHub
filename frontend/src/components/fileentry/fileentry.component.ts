import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { IFileEntry } from '@models/fileentry';

@Component({
    standalone: true,
    selector: 'file-entry',
    templateUrl: './fileentry.component.html',
    styleUrl: './fileentry.component.scss',
    imports: [CommonModule]
})
export class FileEntry {

    @Input() fileEntry: IFileEntry = { name: '', isDir: false, size: 0 };
    @Output() navigateToDirectory = new EventEmitter<string>();

    public entryClicked(entry: IFileEntry) {
        if (entry.isDir) {
            this.navigateToDirectory.emit(entry.name);
        }
    }

    public getFileSize(entry: IFileEntry): string {
        if (entry.isDir) {
            return `${entry.size} items`;
        }
        const gigabytes = Number((entry.size / 1000 / 1000 / 1000).toFixed(2));
        if (gigabytes >= 1) {
            return `${gigabytes} Gb`;
        }
        const megabytes = Number((entry.size / 1000 / 1000).toFixed(2));
        if (megabytes >= 1) {
            return `${megabytes} Mb`;
        }
        const kilobytes = Number((entry.size / 1000).toFixed(2));
        if (kilobytes >= 1) {
            return `${kilobytes} Kb`;
        }
        if (entry.size >= 1) {
            return `${entry.size} bytes`
        }
        return "";
    }
}
