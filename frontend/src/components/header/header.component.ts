import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { IPathSegment } from '@models/IPathSegment';
import { PathService } from '@services/path.service';


@Component({
    standalone: true,
    selector: 'global-header',
    templateUrl: './header.component.html',
    styleUrl: './header.component.scss',
    imports: [CommonModule]
})
export class GlobalHeader implements OnInit {

    @Input() public path: string[] = [];
    @Output() navigateToSegment = new EventEmitter<{ segment: string, last: boolean }>();

    constructor(private pathService: PathService) { }

    public ngOnInit(): void {
        this.pathService.path$.subscribe(path => {
            this.path = path;
        });
    }

    public segmentChange(segment: IPathSegment) {
        this.pathService.segmentChange(segment)
    }
}
