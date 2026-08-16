import { Injectable } from "@angular/core";
import { IPublicPath } from "@models/IPublicPath";
import { BehaviorSubject, Subject, subscribeOn } from "rxjs";

@Injectable({ providedIn: 'root' })
export class PathService {

    private readonly initial = { NextSegment: "", Id: 0, Name: "home", IsBasePath: true, IsDir: true, Size: 0, ItemId: "" };

    private pathSubject = new BehaviorSubject<IPublicPath[]>([this.initial]);
    private segmentNavigationSubject = new Subject<IPublicPath>();

    public NextSegment$ = this.pathSubject.asObservable();
    public segmentNavigation$ = this.segmentNavigationSubject.asObservable();

    constructor() {
        window.addEventListener('popstate', (event) => {
            const state = event.state;
            if (state && state.pathSegments) {
                this.pathSubject.next(state.pathSegments);
                if (state.pathSegments.length) {
                    this.segmentNavigationSubject.next(state.pathSegments[state.pathSegments.length - 1]);
                }
            }
        });

        history.replaceState({ pathSegments: this.pathSubject.value }, '');
    }

    public getCurrentPath(): IPublicPath[] {
        return this.pathSubject.value;
    }

    public updateData(newVal: IPublicPath) {
        const currentPath = this.pathSubject.value;

        if (currentPath.find(x => x.ItemId === newVal.ItemId)) {
            return;
        }

        const newPath = [...currentPath, newVal];

        this.pathSubject.next(newPath);
        history.pushState({ pathSegments: newPath }, '');
    }

    public segmentChange(segment: IPublicPath) {
        const NextSegment = this.pathSubject.value;
        const newPath = NextSegment.slice(0, NextSegment.findIndex(x => x.ItemId === segment.ItemId) + 1);

        this.pathSubject.next(newPath);
        this.segmentNavigationSubject.next(segment);
        history.pushState({ pathSegments: newPath }, '');
    }

    public static getPathName(path: string): string {
        return path.substring(path.lastIndexOf("/") + 1, path.length);
    }
}