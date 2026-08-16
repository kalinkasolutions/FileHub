import { CommonModule } from '@angular/common';
import { AfterViewInit, Component, ElementRef, ViewChild } from '@angular/core';


@Component({
    standalone: true,
    selector: 'file-notfound',
    templateUrl: './notfound.component.html',
    styleUrl: './notfound.component.scss',
    imports: [CommonModule]
})
export class NotFoundComponent {

    public readonly theBeaverText = "this is the beaver.. if you found the beaver, you missed what you were looking for.";
}