import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { IPublicPath } from '@models/IPublicPath';
import { PathService } from '@services/path.service';


@Component({
    standalone: true,
    selector: 'global-header',
    templateUrl: './header.component.html',
    styleUrl: './header.component.scss',
    imports: [CommonModule]
})
export class GlobalHeader implements OnInit {

    @Input() public path: IPublicPath[] = [];
    @Output() navigateToSegment = new EventEmitter<IPublicPath>();

    constructor(private pathService: PathService) { }

    public ngOnInit(): void {
        this.pathService.NextSegment$.subscribe(path => {
            this.path = path;
        });
    }

    public segmentChange(segment: IPublicPath, last: boolean) {
        if (last) {
            return;
        }
        this.pathService.segmentChange(segment)
    }
}
