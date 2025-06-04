import { CommonModule } from '@angular/common';
import { AfterViewInit, Component, ElementRef, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { FileEntry } from '@components/fileentry/fileentry.component';
import { IPublicPath } from '@models/IPublicPath';
import { DirectoryService } from '@services/directory.service';
import { PathService } from '@services/path.service';
import { takeUntil } from 'rxjs';
import { Subject } from 'rxjs/internal/Subject';

@Component({
  standalone: true,
  selector: 'directory-list',
  templateUrl: './directorylist.component.html',
  styleUrl: 'directorylist.component.scss',
  imports: [CommonModule, FileEntry]
})
export class DirectoryList implements OnInit, AfterViewInit, OnDestroy {
  @ViewChild('sentinel') sentinel!: ElementRef;

  private allEntries: IPublicPath[] = [];
  private itemsPerPage = 50;
  private destroy$ = new Subject<void>();


  public displayedEntries: IPublicPath[] = [];

  constructor(private directoryService: DirectoryService, private pathService: PathService) { }

  public ngOnInit(): void {
    this.loadInitial()

    this.pathService.segmentNavigation$.pipe(takeUntil(this.destroy$)).subscribe(x => {
      if (x.ItemId === "" && x.Id === 0) {
        // intial home item
        this.loadInitial()
        return;
      }
      this.directoryService.navigate(x).subscribe(navigation => {
        this.loadEntries(navigation.Entries)
      });
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

  public ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  public navigateToDirectory(publicPath: IPublicPath) {
    this.pathService.updateData(publicPath);
    this.directoryService.navigate(publicPath).subscribe(navigation => {
      this.loadEntries(navigation.Entries)
    });
  }

  private loadInitial() {
    this.directoryService.get().subscribe(entries => {
      this.loadEntries(entries)
    });
  }

  private loadMoreItems() {
    const nextItems = this.allEntries.slice(this.displayedEntries.length, this.displayedEntries.length + this.itemsPerPage);
    this.displayedEntries = [...this.displayedEntries, ...nextItems];
  }

  private loadEntries(entries: IPublicPath[]) {
    this.allEntries = entries;
    this.displayedEntries = [];
    this.loadMoreItems();
  }

}


