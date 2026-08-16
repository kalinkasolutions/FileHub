import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, ElementRef, EventEmitter, inject, Input, OnDestroy, OnInit, Output, ViewChild } from '@angular/core';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { IPublicPath } from '@models/IPublicPath';
import { PathService } from '@services/path.service';
import { Subject, takeUntil } from 'rxjs';


@Component({
    standalone: true,
    selector: 'global-header',
    templateUrl: './header.component.html',
    styleUrl: './header.component.scss',
    imports: [CommonModule, RouterModule]
})
export class GlobalHeader implements OnInit, OnDestroy {

    @Input() public path: IPublicPath[] = [];
    @Output() navigateToSegment = new EventEmitter<IPublicPath>();
    @ViewChild('pathSegmentsContainer') pathSegmentsContainer!: ElementRef;

    public showPathSegments = false;
    public showHeader = true;

    private destroy$ = new Subject<void>();

    // The app is zoneless, so a field written from an rxjs callback is invisible to change
    // detection until the view is told. Every legacy screen carries this until it is rebuilt
    // on signals.
    private readonly cdr = inject(ChangeDetectorRef);

    constructor(private pathService: PathService, private route: ActivatedRoute, private router: Router) { }

    public ngOnInit(): void {
        this.pathService.NextSegment$.pipe(takeUntil(this.destroy$)).subscribe(path => {
            this.path = path;
            this.cdr.markForCheck();
        });

        this.route.data.subscribe(data => {
            this.showPathSegments = data["showPathSegments"] ?? this.showPathSegments;
            this.showHeader = data["showHeader"] ?? this.showHeader;
            this.cdr.markForCheck();
        });
    }

    ngAfterViewChecked() {
        if (this.pathSegmentsContainer) {
            const el = this.pathSegmentsContainer.nativeElement;
            el.scrollLeft = el.scrollWidth;
        }
    }

    public ngOnDestroy(): void {
        this.destroy$.next();
        this.destroy$.complete();
    }

    public isActive(route: string) {
        return this.router.url === route;
    }

    public segmentChange(segment: IPublicPath, last: boolean) {
        if (last) {
            return;
        }
        this.pathService.segmentChange(segment)
    }
}
