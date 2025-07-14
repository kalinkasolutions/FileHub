import { HttpClient } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { INavigation } from "@models/INavigation";
import { IPublicPath } from "@models/IPublicPath";
import { environment } from "environments/environment";
import { Observable } from "rxjs";

@Injectable({
    providedIn: 'root'
})
export class DirectoryService {

    constructor(private httpClient: HttpClient) { }

    public get(): Observable<IPublicPath[]> {
        return this.httpClient.get<IPublicPath[]>(`${environment.apiUrl}/api/files`);
    }

    public navigate(publicPath: IPublicPath) {
        return this.httpClient.post<INavigation>(`${environment.apiUrl}/api/files/navigate`, {
            Id: publicPath.Id,
            Path: publicPath.NextSegment
        });
    }
}