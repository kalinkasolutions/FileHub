import { HttpClient } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { IAdminShare } from "@models/IAdminShare";
import { IPublicPath } from "@models/IPublicPath";
import { IShare } from "@models/IShare";
import { IShareLink } from "@models/IShareLink";
import { catchError, Observable, throwError } from "rxjs";
import { BaseService } from "./base.service";
import { NotificationService } from "./notification.service";

@Injectable({ providedIn: 'root' })
export class ShareService extends BaseService {

    constructor(private httpClient: HttpClient, notificationService: NotificationService) {
        super(notificationService);
    }

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

    public getShares() {
        return this.httpClient.get<IAdminShare[]>("http://localhost:4122/api/admin/shares");
    }

    public delete(path: IAdminShare): Observable<IAdminShare> {
        return this.httpClient.delete<IAdminShare>("http://localhost:4122/api/admin/share", {
            body: path
        }).pipe(catchError(this.handleError));
    }
}