import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit } from '@angular/core';
import { INotifcationModel as INotificationModel, INotification, NotificationLevel } from '@models/INotifcation';
import { NotificationService } from '@services/notification.service';
import { Subject, takeUntil } from 'rxjs';

@Component({
    standalone: true,
    selector: 'app-notification',
    templateUrl: './notification.component.html',
    styleUrl: './notification.component.scss',
    imports: [CommonModule]
})
export class Notification implements OnInit, OnDestroy {

    public notifications: INotificationModel[] = [];

    private readonly defaultDuration = 5000;
    private readonly animationDuration = 300;
    private destroy$ = new Subject<void>();

    constructor(private notificationService: NotificationService) { }

    public ngOnInit(): void {
        this.notificationService.notifcations$.pipe(takeUntil(this.destroy$)).subscribe((notification) => {
            this.addNotification(notification);
        });
    }

    public ngOnDestroy(): void {
        this.destroy$.next();
        this.destroy$.complete();
    }

    public kill(notification: INotificationModel) {
        this.notifications = this.notifications.filter(x => x.id !== notification.id);
    }

    public getLevel(notifacion: INotification): string {
        switch (notifacion.level) {
            case NotificationLevel.error:
                return "error"
            case NotificationLevel.info:
                return "info"
            case NotificationLevel.success:
                return "success"
        }
    }

    private addNotification(notification: INotification) {
        const notificationModel = { ...notification, duration: notification.duration ?? this.defaultDuration, id: crypto.randomUUID(), dissapearing: false, };
        this.notifications.push(notificationModel);

        setTimeout(() => {
            this.notifications.forEach(notification => {
                if (notification.id == notificationModel.id) {
                    notification.dissapearing = true;
                }
            });
        }, notificationModel.duration - this.animationDuration);

        setTimeout(() => {
            this.notifications = this.notifications.filter(x => x.id !== notificationModel.id)
        }, notificationModel.duration);
    }
}