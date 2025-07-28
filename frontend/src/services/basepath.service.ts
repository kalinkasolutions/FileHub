import { HttpClient } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { IBasePath } from "@models/IBasePath";
import { catchError, Observable, throwError } from "rxjs";
import { NotificationService } from "./notification.service";
import { BaseService } from "./base.service";
import { environment } from "@env/environment";

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
        return this.httpClient.post<IBasePath>(`${environment.apiUrl}/api/admin/base-path`, path).pipe(catchError(this.handleError));
    }


    public getBasePaths(): Observable<IBasePath[]> {
        return this.httpClient.get<IBasePath[]>(`${environment.apiUrl}/api/admin/base-path`).pipe(catchError(this.handleError));
    }

    public update(path: IBasePath): Observable<IBasePath> {
        return this.httpClient.put<IBasePath>(`${environment.apiUrl}/api/admin/base-path`, path).pipe(catchError(this.handleError));
    }

    public delete(path: IBasePath): Observable<IBasePath> {
        return this.httpClient.delete<IBasePath>(`${environment.apiUrl}/api/admin/base-path`, {
            body: path
        }).pipe(catchError(this.handleError));
    }

}