import { HttpClient } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { IPublicPath } from "@models/IPublicPath";
import { IShare } from "@models/IShare";
import { IShareLink } from "@models/IShareLink";
import { catchError, throwError } from "rxjs";

@Injectable({ providedIn: 'root' })
export class ShareService {

    constructor(private httpClient: HttpClient) { }

    public share(publicPath: IPublicPath) {
        return this.httpClient.post<IShareLink>("http://localhost:4122/api/share/create", {
            Id: publicPath.Id,
            Path: publicPath.NextSegment
        });
    }

    public validateShare(id: string) {

        return this.httpClient.get<IShare>(`http://localhost:4122/public-api/share/validate/${id}`).pipe(
            catchError(error => {
                if (error.url) {
                    window.location = error.url;
                }
                return throwError(() => error);
            })
        );
    }
}