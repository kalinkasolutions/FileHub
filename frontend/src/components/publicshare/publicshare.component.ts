import { CommonModule } from "@angular/common";
import { Component, OnInit } from "@angular/core";
import { Meta, Title } from "@angular/platform-browser";
import { ActivatedRoute } from "@angular/router";
import { IShare } from "@models/IShare";
import { FileService } from "@services/file.service";
import { ShareService } from "@services/share.service";
import { FileSize } from "util/filesize";

@Component({
    standalone: true,
    selector: 'app-publicshare',
    templateUrl: './publicshare.component.html',
    styleUrl: './publicshare.component.scss',
    imports: [CommonModule]
})
export class PublicShare {

    public share: IShare = {
        Id: "",
        Size: 0,
        Name: "",
        IsDir: false
    }

    constructor(private route: ActivatedRoute, private fileService: FileService, private shareService: ShareService) {
        debugger
        this.route.paramMap.subscribe(params => {
            const id = params.get('id') ?? "";
            this.shareService.validateShare(id).subscribe(share => {
                this.share = share;
            });
        });
    }

    public download() {
        this.fileService.downloadPublicShare(this.share);
    }

    public getFileSize(size: number): string {
        return FileSize.FileSize(size);
    }
}