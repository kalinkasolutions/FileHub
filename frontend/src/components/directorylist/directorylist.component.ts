import { CommonModule } from '@angular/common';
import { AfterViewInit, Component, ElementRef, OnInit, ViewChild } from '@angular/core';
import { FileEntry } from '@components/fileentry/fileentry.component';
import { IFileEntry } from '@models/IFileEntry';
import { IPathSegment } from '@models/IPathSegment';
import { DirectoryService } from '@services/directory.service';
import { PathService } from '@services/path.service';

@Component({
  standalone: true,
  selector: 'directory-list',
  templateUrl: './directorylist.component.html',
  styleUrl: 'directorylist.component.scss',
  imports: [CommonModule, FileEntry]
})
export class DirectoryList implements OnInit, AfterViewInit {
  @ViewChild('sentinel') sentinel!: ElementRef;

  private allEntries: IFileEntry[] = [];
  private itemsPerPage = 50;

  public path: string[] = [];
  public displayedEntries: IFileEntry[] = [];

  constructor(private directoryService: DirectoryService, private pathService: PathService) { }

  public ngOnInit(): void {
    this.loadDirectories();
    this.pathService.segment$.subscribe(s => {
      this.navigateToSegment(s);
    })
  }

  public ngAfterViewInit(): void {
    const observer = new IntersectionObserver((entries) => {
      entries.forEach(entry => {
        if (entry.isIntersecting) {
          this.loadMoreItems();
        }
      });
    }, { rootMargin: '300px' });

    observer.observe(this.sentinel.nativeElement);
  }

  private loadMoreItems() {
    const nextItems = this.allEntries.slice(this.displayedEntries.length, this.displayedEntries.length + this.itemsPerPage);
    this.displayedEntries = [...this.displayedEntries, ...nextItems];
  }

  public navigateToDirectory(directoryName: string) {
    this.path.push(directoryName)
    this.loadDirectories();
  }

  public navigateBack() {
    this.path.pop();
    this.loadDirectories();
  }

  public navigateToSegment(segment: IPathSegment) {
    if (segment.last) {
      return;
    }

    this.path = this.path.slice(0, this.path.lastIndexOf(segment.segment) + 1);
    this.loadDirectories();
  }

  private loadDirectories(): void {
    this.directoryService.get(this.path.join("/")).subscribe(entries => {
      this.allEntries = entries;
      this.displayedEntries = [];
      this.loadMoreItems();
    });
    this.pathService.updateData(this.path);
  }

}


