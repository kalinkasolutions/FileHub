import { HttpClient } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { IBasePath } from "@models/IBasePath";
import { catchError, Observable, throwError } from "rxjs";
import { NotificationService } from "./notification.service";
import { NotificationLevel } from "@models/INotifcation";

@Injectable({
    providedIn: 'root'
})
export class AdminService {

    constructor(private httpClient: HttpClient,
        private notificationService: NotificationService
    ) { }

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

    private handleError = (error: any): Observable<never> => {
        this.notificationService.notify({
            level: NotificationLevel.error,
            title: "Request failed",
            message: error.error
        })
        return throwError(() => error);
    }

}