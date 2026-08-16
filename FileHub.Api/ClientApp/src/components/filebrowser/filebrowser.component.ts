import {
  Component,
  ElementRef,
  afterRenderEffect,
  computed,
  effect,
  inject,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { GlobalHeader } from '@components/header/header.component';
import { IFileEntry } from '@models/IFileEntry';
import { apiErrorMessage } from '@services/api-error';
import { DirectoryService } from '@services/directory.service';
import { FileService } from '@services/file.service';
import { IPathSegment, PathService } from '@services/path.service';
import { ToastrService } from 'ngx-toastr';
import { finalize, map } from 'rxjs';
import { formatEntrySize } from './format';
import { IShareDialogData, ShareDialogComponent } from './share-dialog.component';
import { ShareLinksComponent } from './share-links.component';

/** The two halves of the browsing UI: the tree, and the links the caller has made from it. */
type BrowserTab = 'files' | 'links';

/**
 * How many rows are put in the DOM at a time. A media directory can hold tens of thousands of
 * entries and the API answers with all of them; rendering all of them is what would be slow.
 */
const pageSize = 50;

/**
 * The file browser: the caller's base paths, one directory at a time below them, and the actions on
 * a row (download, share). The breadcrumb and the back button are `PathService`'s trail, which is
 * also the browser's history — see that service.
 */
@Component({
  standalone: true,
  // Not `file-browser`: `_legacy.scss` styles that element name (and every `button` and `input`
  // inside it) for the pre-rewrite markup, which would land on this component too.
  selector: 'app-file-browser',
  templateUrl: './filebrowser.component.html',
  styleUrl: './filebrowser.component.scss',
  imports: [FormsModule, MatIconModule, GlobalHeader, ShareLinksComponent],
})
export class FilebrowserComponent {
  private readonly directoryService = inject(DirectoryService);
  private readonly fileService = inject(FileService);
  private readonly dialog = inject(MatDialog);
  private readonly toastr = inject(ToastrService);

  public readonly pathService = inject(PathService);

  private readonly crumbs = viewChild<ElementRef<HTMLElement>>('crumbs');
  private readonly sentinel = viewChild<ElementRef<HTMLElement>>('sentinel');

  /**
   * Which listing the entries in hand belong to. Navigation is a click away from being faster than
   * the network, so a late answer to an abandoned request is dropped rather than shown.
   */
  private requestId = 0;

  public readonly tab = signal<BrowserTab>('files');
  public readonly entries = signal<IFileEntry[]>([]);
  public readonly isLoading = signal(true);
  public readonly hasFailed = signal(false);
  public readonly search = signal('');
  public readonly shown = signal(pageSize);

  public readonly title = computed(() => this.pathService.current()?.name ?? 'Files');

  public readonly filtered = computed(() => {
    const term = this.search().trim().toLowerCase();
    const entries = this.entries();

    if (term.length === 0) {
      return entries;
    }

    return entries.filter((x) => x.name.toLowerCase().includes(term));
  });

  public readonly visible = computed(() => this.filtered().slice(0, this.shown()));

  public readonly emptyMessage = computed(() => {
    if (this.hasFailed()) {
      return 'This folder could not be read.';
    }

    if (this.search().trim().length > 0) {
      return 'Nothing here matches that.';
    }

    if (this.pathService.isAtTop()) {
      return 'No disks yet — an administrator has to give this account access to one.';
    }

    return 'This folder is empty.';
  });

  public constructor() {
    // The trail is the single source of truth for what is listed, so the listing follows it rather
    // than being fetched at each call site: a breadcrumb click, a step down and the browser's back
    // button all land here the same way.
    effect(() => {
      const segment = this.pathService.current();
      untracked(() => this.load(segment));
    });

    // Re-attached whenever the sentinel comes or goes — it is inside the files tab, so switching
    // tabs removes it from the DOM.
    effect((onCleanup) => {
      const element = this.sentinel()?.nativeElement;

      if (!element) {
        return;
      }

      const observer = new IntersectionObserver(
        (entries) => {
          if (entries.some((x) => x.isIntersecting)) {
            this.showMore();
          }
        },
        { rootMargin: '300px' },
      );

      observer.observe(element);
      onCleanup(() => observer.disconnect());
    });

    // The breadcrumb scrolls sideways; the crumb that matters is the last one, so keep it in view.
    afterRenderEffect(() => {
      this.pathService.segments();
      const element = this.crumbs()?.nativeElement;

      if (!element) {
        return;
      }

      element.scrollLeft = element.scrollWidth;
    });
  }

  public onSearch(term: string): void {
    this.search.set(term);
    this.shown.set(pageSize);
  }

  public showTab(tab: BrowserTab): void {
    this.tab.set(tab);
  }

  public open(entry: IFileEntry): void {
    this.pathService.open(entry);
  }

  public download(entry: IFileEntry): void {
    this.fileService.download(entry);
  }

  public share(entry: IFileEntry): void {
    const data: IShareDialogData = {
      name: entry.name,
      basePathId: entry.id,
      relativePath: entry.nextSegment,
    };

    this.dialog.open(ShareDialogComponent, { data });
  }

  public size(entry: IFileEntry): string {
    return formatEntrySize(entry.isDir, entry.size);
  }

  private showMore(): void {
    if (this.shown() >= this.filtered().length) {
      return;
    }

    this.shown.update((x) => x + pageSize);
  }

  /** Null lists the caller's base paths; anything else lists one directory below one of them. */
  private load(segment: IPathSegment | null): void {
    const id = this.requestId + 1;
    this.requestId = id;

    this.isLoading.set(true);
    this.hasFailed.set(false);
    this.search.set('');
    this.shown.set(pageSize);

    const request = segment
      ? this.directoryService
          .navigate(segment.basePathId, segment.path)
          .pipe(map((navigation) => navigation.entries))
      : this.directoryService.getBasePaths();

    request.pipe(finalize(() => this.settle(id))).subscribe({
      next: (entries) => {
        if (id === this.requestId) {
          this.entries.set(entries);
        }
      },
      error: (error: unknown) => {
        if (id !== this.requestId) {
          return;
        }

        this.entries.set([]);
        this.hasFailed.set(true);
        this.toastr.error(apiErrorMessage(error, 'Could not open that folder'));
      },
    });
  }

  private settle(id: number): void {
    if (id === this.requestId) {
      this.isLoading.set(false);
    }
  }
}
