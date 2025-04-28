import { HttpClient } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { IFileEntry } from "@models/fileentry";
import { BehaviorSubject, Observable } from "rxjs";

@Injectable({
    providedIn: 'root'
})
export class DirectoryService {

    constructor(private httpClient: HttpClient) { }

    public get(directoryName = ""): Observable<IFileEntry[]> {
        return this.httpClient.get<IFileEntry[]>(`http://localhost:4122/admin/files?directoryName=${directoryName}`);
    }

    public downoadAsZip() {
        return this.httpClient.get<any>("http://localhost:4122/admin/files/download-folder");
    }
}