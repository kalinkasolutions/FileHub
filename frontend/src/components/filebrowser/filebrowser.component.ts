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
import { IFileEntry } from '@models/IFileEntry';
import { apiErrorMessage } from '@services/api-error';
import { AuthService } from '@services/auth.service';
import { DirectoryService } from '@services/directory.service';
import { FileService } from '@services/file.service';
import { IPathSegment, PathService } from '@services/path.service';
import { ToastrService } from 'ngx-toastr';
import { finalize, map } from 'rxjs';
import { formatEntrySize } from '@util/format';
import { IShareDialogData, ShareDialogComponent } from './share-dialog.component';
import { ReceivedSharesComponent } from './received-shares.component';
import { ShareLinksComponent } from './share-links.component';

/**
 * The browsing UI's three views: the tree, the links the caller has handed out from it, and the
 * links aimed at a group they belong to. The last one is not a subset of the first — a group link
 * can point into a base path the caller holds no grant on, so its target is in no listing of theirs.
 */
type BrowserTab = 'files' | 'links' | 'shared';

/**
 * How many rows are put in the DOM at a time. A media directory can hold tens of thousands of
 * entries and the API answers with all of them; rendering all of them is what would be slow.
 */
const pageSize = 50;

/**
 * The file browser: the caller's base paths, one directory at a time below them, and the actions on
 * a row (download, share). The breadcrumb and the back button are `PathService`'s trail, which is
 * also the browser's history — see that service.
 *
 * The trail is the listing panel's heading rather than a row under one: the folder's name is the
 * last crumb, so a separate title said the same thing a second time.
 */
@Component({
  standalone: true,
  // Not `file-browser`: `_legacy.scss` styles that element name (and every `button` and `input`
  // inside it) for the pre-rewrite markup, which would land on this component too.
  selector: 'app-file-browser',
  templateUrl: './filebrowser.component.html',
  styleUrl: './filebrowser.component.scss',
  imports: [FormsModule, MatIconModule, ShareLinksComponent, ReceivedSharesComponent],
})
export class FilebrowserComponent {
  private readonly directoryService = inject(DirectoryService);
  private readonly fileService = inject(FileService);
  private readonly dialog = inject(MatDialog);
  private readonly toastr = inject(ToastrService);
  private readonly authService = inject(AuthService);

  public readonly pathService = inject(PathService);

  private readonly crumbs = viewChild<ElementRef<HTMLElement>>('crumbs');
  private readonly scroller = viewChild<ElementRef<HTMLElement>>('scroller');
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

  public readonly filtered = computed(() => {
    const term = this.search().trim().toLowerCase();
    const entries = this.entries();

    if (term.length === 0) {
      return entries;
    }

    return entries.filter((x) => x.name.toLowerCase().includes(term));
  });

  public readonly visible = computed(() => this.filtered().slice(0, this.shown()));

  /**
   * The tally in the panel's heading. It counts what the filter left, against what the folder holds,
   * because a filtered listing otherwise gives no sign of how much of the folder it is hiding.
   */
  public readonly count = computed(() => {
    if (this.isLoading()) {
      return '';
    }

    const total = this.entries().length;
    const matched = this.filtered().length;

    if (matched !== total) {
      return `${matched} of ${total}`;
    }

    return total === 1 ? '1 item' : `${total} items`;
  });

  public readonly emptyMessage = computed(() => {
    if (this.hasFailed()) {
      return 'This folder could not be read.';
    }

    if (this.search().trim().length > 0) {
      return 'Nothing here matches that.';
    }

    // An admin reaches every base path by the role alone, so an empty top level cannot mean a
    // missing grant for them — it means nobody has added a disk yet, which is their own job.
    if (this.pathService.isAtTop() && this.authService.isAdmin()) {
      return 'No disks yet — add one in the admin area and it turns up here.';
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
    //
    // The root is the list, not the viewport. The sentinel is the last row of a scroller that runs
    // to the bottom edge of the screen, so against the viewport it is clipped away by the list's own
    // overflow at exactly the moment it should fire, and `rootMargin` — which grows the root's rect,
    // not an ancestor's clip — never gets a chance to see it. Rendering then stops at one page.
    effect((onCleanup) => {
      const element = this.sentinel()?.nativeElement;
      const root = this.scroller()?.nativeElement;

      if (!element || !root) {
        return;
      }

      const observer = new IntersectionObserver(
        (entries) => {
          if (entries.some((x) => x.isIntersecting)) {
            this.showMore();
          }
        },
        { root, rootMargin: '300px' },
      );

      observer.observe(element);
      onCleanup(() => observer.disconnect());
    });

    // The trail scrolls sideways; the crumb that matters is the last one, so keep it in view. It is
    // in the panel's heading now, which is narrower than the row it replaced — a deep path runs off
    // the right of it sooner, so this matters more than it did.
    //
    // `count()` is read for its width, not its value. It shares the heading with the trail and goes
    // from empty to "5 items" when the listing lands, which is *after* the navigation that moved the
    // trail: the tally appearing narrows the trail beside it, and a scroll already at the end stops
    // being at the end. On a phone that left the folder you are standing in half off the edge.
    afterRenderEffect(() => {
      this.pathService.segments();
      this.count();
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

  /**
   * Whether this account may publish a link. Without it there is no share button and no Links tab —
   * losing the role revokes the links, so there is nothing behind that tab to reach.
   */
  public readonly canShare = computed(() => this.authService.canCreateShares());

  public showTab(tab: BrowserTab): void {
    if (tab === 'links' && !this.canShare()) {
      return;
    }

    // 'shared' needs no guard: receiving a link is not publishing one, so an account without
    // CreateShares reaches that tab even though its Links tab is hidden.
    this.tab.set(tab);
  }

  public open(entry: IFileEntry): void {
    this.pathService.open(entry);
  }

  public download(entry: IFileEntry): void {
    this.fileService.download(entry);
  }

  public share(entry: IFileEntry): void {
    if (!this.canShare()) {
      return;
    }

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
