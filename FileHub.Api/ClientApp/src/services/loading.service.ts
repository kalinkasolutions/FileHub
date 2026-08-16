import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class LoadingService {
    private loadingCount = 0;
    public loading$ = new BehaviorSubject(false);

    show() {
        this.loadingCount++;
        setTimeout(() => this.loading$.next(true));
    }

    hide() {
        this.loadingCount = Math.max(0, this.loadingCount - 1);
        if (this.loadingCount === 0) {
            setTimeout(() => this.loading$.next(false));
        }
    }
}