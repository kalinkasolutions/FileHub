import { Injectable } from "@angular/core";
import { IPublicPath } from "@models/IPublicPath";
import { IShare } from "@models/IShare";

@Injectable({
    providedIn: 'root'
})
export class FileService {

    public download(publicPath: IPublicPath) {
        const link = document.createElement('a');
        link.href = `http://localhost:4122/api/files/download/${encodeURIComponent(publicPath.Id)}/${encodeURIComponent(publicPath.NextSegment)}`;
        link.download = publicPath.Name;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
    }

    public downloadPublicShare(publicShare: IShare) {
        const link = document.createElement('a');
        link.href = `http://localhost:4122/public-api/files/download/${publicShare.Id}`;
        link.download = publicShare.Name;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
    }
} 