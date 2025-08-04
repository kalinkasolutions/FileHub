import { CommonModule } from '@angular/common';
import { AfterViewInit, Component, ElementRef, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { FileEntry } from '@components/fileentry/fileentry.component';
import { INavigation } from '@models/INavigation';
import { IPublicPath } from '@models/IPublicPath';
import { DirectoryService } from '@services/directory.service';
import { PathService } from '@services/path.service';
import { debounceTime, takeUntil } from 'rxjs';
import { Subject } from 'rxjs/internal/Subject';

@Component({
  standalone: true,
  selector: 'directory-list',
  templateUrl: './directorylist.component.html',
  styleUrl: 'directorylist.component.scss',
  imports: [CommonModule, FileEntry, FormsModule]
})
export class DirectoryList implements OnInit, AfterViewInit, OnDestroy {
  @ViewChild('sentinel') sentinel!: ElementRef;

  private allEntries: IPublicPath[] = [];
  private filteredEntries: IPublicPath[] = [];
  private itemsPerPage = 50;
  private destroy$ = new Subject<void>();
  private searchSubject = new Subject<string>();

  public displayedEntries: IPublicPath[] = [];
  public searchTerm: string = "";

  constructor(private directoryService: DirectoryService, private pathService: PathService) {
    this.searchSubject
      .pipe(debounceTime(300))
      .pipe(takeUntil(this.destroy$))
      .subscribe(value => {
        this.filteredEntries = this.allEntries.filter(x => x.Name.toLowerCase().startsWith(value.toLowerCase()));
        this.displayedEntries = [];
        this.loadMoreItems();
      });
  }

  public ngOnInit(): void {
    const currentPath = this.pathService.getCurrentPath();
    if (currentPath && currentPath.length > 0) {
      const lastSegment = currentPath[currentPath.length - 1];
      if (lastSegment.ItemId === '' && lastSegment.Id === 0) {
        this.loadInitial();
      } else {
        this.directoryService.navigate(lastSegment).subscribe(navigation => {
          this.navigate(navigation);
        });
      }
    }

    this.pathService.segmentNavigation$
      .pipe(takeUntil(this.destroy$))
      .subscribe(x => {
        if (x.ItemId === '' && x.Id === 0) {
          this.loadInitial();
          return;
        }
        this.directoryService.navigate(x).subscribe(navigation => {
          this.navigate(navigation);
        });
      });
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

  public ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  public navigateToDirectory(publicPath: IPublicPath) {
    this.pathService.updateData(publicPath);
    this.directoryService.navigate(publicPath).subscribe(navigation => {
      this.navigate(navigation);
    });
  }

  private loadInitial() {
    this.searchTerm = "";
    this.directoryService.get().subscribe(entries => {
      this.allEntries = entries;
      this.filteredEntries = entries;
      this.displayedEntries = [];
      this.loadMoreItems()
    });
  }

  private loadMoreItems() {
    const nextItems = this.filteredEntries.slice(this.displayedEntries.length, this.displayedEntries.length + this.itemsPerPage);
    this.displayedEntries = [...this.displayedEntries, ...nextItems];
  }

  private navigate(navigation: INavigation) {
    this.searchTerm = "";
    this.allEntries = navigation.Entries;
    this.filteredEntries = navigation.Entries;
    this.displayedEntries = [];
    this.loadMoreItems();
  }

  public search() {
    this.searchSubject.next(this.searchTerm);
  }

}


