import { HttpClient } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { IPublicPath } from "@models/IPublicPath";

@Injectable({
    providedIn: 'root'
})
export class FileService {

    constructor(private httpClient: HttpClient) { }


    public downloadFile(publicPath: IPublicPath) {
        const link = document.createElement('a');
        link.href = `http://localhost:4122/api/files/download-file/${encodeURIComponent(publicPath.Id)}/${encodeURIComponent(publicPath.NextSegment)}`;
        link.download = publicPath.Name;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
    }

    public downoadAsZip() {
        return this.httpClient.get<any>("http://localhost:4122/api/files/download-folder");
    }
} 