import { Injectable } from '@angular/core';
import { INotification, NotificationLevel } from '@models/INotifcation';
import { Subject } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class NotificationService {
  private notificationSubject = new Subject<INotification>();

  public notifcations$ = this.notificationSubject.asObservable();

  public notify(notification: INotification) {
    this.notificationSubject.next(notification);
  }
}
