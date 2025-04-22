import { Injectable } from "@angular/core";
import { IPathSegment } from "@models/pathsegment";
import { BehaviorSubject } from "rxjs";

@Injectable({ providedIn: 'root' })
export class PathService {

    private pathSubject = new BehaviorSubject<string[]>([]);
    path$ = this.pathSubject.asObservable();

    private segmentSubject = new BehaviorSubject<IPathSegment>({ segment: "", last: false });
    segment$ = this.segmentSubject.asObservable();

    public updateData(newVal: string[]) {
        this.pathSubject.next(newVal);
    }

    public segmentChange(segment: IPathSegment) {
        this.segmentSubject.next(segment);
    }
}