import { HttpClient } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { INavigation } from "@models/INavigation";
import { IPublicPath } from "@models/IPublicPath";
import { Observable } from "rxjs";

@Injectable({
    providedIn: 'root'
})
export class DirectoryService {

    constructor(private httpClient: HttpClient) { }

    public get(): Observable<IPublicPath[]> {
        return this.httpClient.get<IPublicPath[]>(`http://localhost:4122/api/files`);
    }

    public navigate(publicPath: IPublicPath) {
        return this.httpClient.post<INavigation>(`http://localhost:4122/api/files/navigate`, {
            Id: publicPath.Id,
            Path: publicPath.NextSegment
        });
    }
}