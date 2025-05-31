import { Injectable } from "@angular/core";
import { IPublicPath } from "@models/IPublicPath";
import { BehaviorSubject, Subject, subscribeOn } from "rxjs";

@Injectable({ providedIn: 'root' })
export class PathService {

    private pathSubject = new BehaviorSubject<IPublicPath[]>([{ NextSegment: "", Id: 0, Name: "home", IsBasePath: true, IsDir: true, Size: 0, ItemId: "" }]);
    private segmentNavigationSubject = new Subject<IPublicPath>();

    public NextSegment$ = this.pathSubject.asObservable();
    public segmentNavigation$ = this.segmentNavigationSubject.asObservable();

    public updateData(newVal: IPublicPath) {
        const currentPath = this.pathSubject.value;

        if (currentPath.find(x => x.ItemId === newVal.ItemId)) {
            return;
        }

        this.pathSubject.next([...currentPath, newVal]);
    }

    public segmentChange(segment: IPublicPath) {
        const NextSegment = this.pathSubject.value;
        const newPath = NextSegment.slice(0, NextSegment.findIndex(x => x.ItemId === segment.ItemId) + 1);

        this.pathSubject.next(newPath);
        this.segmentNavigationSubject.next(segment);
    }
}