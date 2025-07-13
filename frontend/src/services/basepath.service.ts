import { HttpClient } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { IBasePath } from "@models/IBasePath";
import { catchError, Observable, throwError } from "rxjs";
import { NotificationService } from "./notification.service";
import { BaseService } from "./base.service";

@Injectable({
    providedIn: 'root'
})
export class BasePathService extends BaseService {

    constructor(private httpClient: HttpClient,
        notificationService: NotificationService
    ) {
        super(notificationService);
    }

    public insert(path: IBasePath): Observable<IBasePath> {
        return this.httpClient.post<IBasePath>("http://localhost:4122/api/admin/base-path", path).pipe(catchError(this.handleError));
    }


    public getBasePaths(): Observable<IBasePath[]> {
        return this.httpClient.get<IBasePath[]>("http://localhost:4122/api/admin/base-path").pipe(catchError(this.handleError));
    }

    public update(path: IBasePath): Observable<IBasePath> {
        return this.httpClient.put<IBasePath>("http://localhost:4122/api/admin/base-path", path).pipe(catchError(this.handleError));
    }

    public delete(path: IBasePath): Observable<IBasePath> {
        return this.httpClient.delete<IBasePath>("http://localhost:4122/api/admin/base-path", {
            body: path
        }).pipe(catchError(this.handleError));
    }

}