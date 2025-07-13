import { Observable, throwError } from "rxjs";
import { NotificationService } from "./notification.service";
import { NotificationLevel } from "@models/INotifcation";

export abstract class BaseService {

    constructor(private notificationService: NotificationService) { }

    protected handleError = (error: any): Observable<never> => {
        this.notificationService.notify({
            level: NotificationLevel.error,
            title: "Request failed",
            message: error.error
        })
        return throwError(() => error);
    }
}