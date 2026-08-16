export enum NotificationLevel {
    info,
    success,
    error
}

export interface INotification {
    title: string;
    message: string;
    level: NotificationLevel;
    duration?: number;
}

export interface INotifcationModel extends INotification {
    id: string;
    dissapearing: boolean;
}