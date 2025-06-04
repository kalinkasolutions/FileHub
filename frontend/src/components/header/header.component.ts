import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnDestroy, OnInit, Output } from '@angular/core';
import { IPublicPath } from '@models/IPublicPath';
import { PathService } from '@services/path.service';
import { Subject, takeUntil } from 'rxjs';


@Component({
    standalone: true,
    selector: 'global-header',
    templateUrl: './header.component.html',
    styleUrl: './header.component.scss',
    imports: [CommonModule]
})
export class GlobalHeader implements OnInit, OnDestroy {

    @Input() public path: IPublicPath[] = [];
    @Output() navigateToSegment = new EventEmitter<IPublicPath>();

    private destroy$ = new Subject<void>();

    constructor(private pathService: PathService) { }

    public ngOnInit(): void {
        this.pathService.NextSegment$.pipe(takeUntil(this.destroy$)).subscribe(path => {
            this.path = path;
        });
    }

    public ngOnDestroy(): void {
        this.destroy$.next();
        this.destroy$.complete();
    }

    public segmentChange(segment: IPublicPath, last: boolean) {
        if (last) {
            return;
        }
        this.pathService.segmentChange(segment)
    }
}
