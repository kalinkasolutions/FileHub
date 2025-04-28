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

    @Input() entry: IFileEntry = { name: '', isDir: false, size: 0 };
    @Input() currentPath: string[] = [];
    @Output() navigateToDirectory = new EventEmitter<string>();

    public entryClicked() {
        if (this.entry.isDir) {
            this.navigateToDirectory.emit(this.entry.name);
        }
    }

    public shareEntry() { }

    public downloadEntry() {
        
     }


    public getFileSize(): string {
        if (this.entry.isDir) {
            return `${this.entry.size} ${this.entry.size === 1 ? "item" : "items"}`;
        }
        const gigabytes = Number((this.entry.size / 1000 / 1000 / 1000).toFixed(2));
        if (gigabytes >= 1) {
            return `${gigabytes} Gb`;
        }
        const megabytes = Number((this.entry.size / 1000 / 1000).toFixed(2));
        if (megabytes >= 1) {
            return `${megabytes} Mb`;
        }
        const kilobytes = Number((this.entry.size / 1000).toFixed(2));
        if (kilobytes >= 1) {
            return `${kilobytes} Kb`;
        }
        if (this.entry.size >= 1) {
            return `${this.entry.size} ${this.entry.size === 1 ? "byte" : "bytes"}`
        }
        return "";
    }
}
