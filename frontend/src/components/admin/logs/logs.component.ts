import { DatePipe } from '@angular/common';
import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatIcon } from '@angular/material/icon';
import { ILogEntry, ILogQuery, levelClass, logLevels } from '@models/ILogEntry';
import { apiErrorMessage } from '@services/api-error';
import { LogService, LogStreamState } from '@services/log.service';
import { ToastrService } from 'ngx-toastr';

/**
 * How many lines the screen holds. The table has no retention and can be very large, so the buffer
 * is bounded on the client as well as on the server — following a busy install for an hour would
 * otherwise grow the DOM without limit.
 */
const maxBuffered = 1000;

/**
 * The server log. Read-only, admin-only, and the one screen that answers "what has this install
 * been doing" without shelling into the container.
 *
 * <p>It is <b>pushed, not polled</b>. A SignalR hub tells the screen that something was written
 * and the screen answers with a request for whatever is newer than the highest id it holds; an idle
 * installation costs no requests at all. The hub sends a bare signal rather than the entries — the
 * filter and the ids stay server-side, so there is one implementation of each. See
 * <code>LogHub</code>.</p>
 *
 * <p>Two modes, and the difference matters:</p>
 * <ul>
 *   <li><b>Following</b> — connected to the hub; new lines are prepended as they are announced.</li>
 *   <li><b>Paused</b> — the hub stays connected but its signals are ignored, so the list holds
 *   still while it is being read. Any filter change pauses nothing; it just re-asks from
 *   scratch.</li>
 * </ul>
 *
 * <p>Following is switched off automatically whenever a date range is set: "the newest lines" and
 * "the lines between these two times" are different questions, and tailing a closed range would
 * append entries that fall outside it.</p>
 */
@Component({
  selector: 'admin-logs',
  standalone: true,
  imports: [DatePipe, FormsModule, MatIcon],
  templateUrl: 'logs.component.html',
  styleUrl: 'logs.component.scss',
})
export class LogsComponent implements OnInit, OnDestroy {
  private readonly logService = inject(LogService);
  private readonly toastr = inject(ToastrService);

  /** The picker's options, least severe first. */
  public readonly levels = logLevels;

  public readonly entries = signal<ILogEntry[]>([]);
  public readonly totalCount = signal(0);
  public readonly hasMore = signal(false);
  public readonly isLoading = signal(true);

  // The filter. Held as separate signals rather than one object so a template two-way binding
  // writes exactly one of them.
  public readonly minLevel = signal('');
  public readonly search = signal('');
  /** `datetime-local` values — local wall-clock, with no zone. See `toIsoUtc`. */
  public readonly from = signal('');
  public readonly to = signal('');

  public readonly isFollowing = signal(true);

  /** The live channel's own health, so a view that has stopped being live can say so. */
  public readonly streamState = signal<LogStreamState>('connecting');

  /** Which entry has its exception expanded; only one at a time. */
  public readonly expandedId = signal<number | null>(null);

  /** Closes the hub connection. Null until it has been opened. */
  private disconnect: (() => void) | null = null;

  /**
   * Guards against a slow answer landing after the filter it belongs to has been replaced — the
   * same reason the file browser carries one.
   */
  private requestId = 0;

  /** True once a date range narrows the view, which is what takes following off the table. */
  public readonly hasRange = computed(() => this.from().length > 0 || this.to().length > 0);

  public readonly isFiltered = computed(
    () => this.minLevel().length > 0 || this.search().trim().length > 0 || this.hasRange(),
  );

  public ngOnInit(): void {
    this.reload();

    // Connected once, for the life of the screen — including while paused, so resuming is instant
    // and does not depend on a socket handshake.
    this.disconnect = this.logService.connect(
      () => this.onLogged(),
      (state) => this.streamState.set(state),
    );
  }

  public ngOnDestroy(): void {
    this.disconnect?.();
    this.disconnect = null;
  }

  /**
   * The hub says a line was written. Only fetch when this screen is actually following and is not
   * pinned to a closed date range — a signal is about "now", which a historical view is not asking
   * about.
   */
  private onLogged(): void {
    if (!this.isFollowing() || this.hasRange()) {
      return;
    }

    this.tail();
  }

  /** Any filter change throws the buffer away and asks again: the old lines answered another question. */
  public applyFilter(): void {
    // A closed range and "follow the newest" contradict each other, so setting one drops the other.
    if (this.hasRange()) {
      this.isFollowing.set(false);
    }

    this.reload();
  }

  public clearFilter(): void {
    this.minLevel.set('');
    this.search.set('');
    this.from.set('');
    this.to.set('');
    this.reload();
  }

  public toggleFollow(): void {
    if (this.hasRange()) {
      return;
    }

    const following = !this.isFollowing();
    this.isFollowing.set(following);

    if (following) {
      // Catch up on whatever arrived while it was paused. From here the hub drives it.
      this.tail();
    }
  }

  public toggleException(entry: ILogEntry): void {
    if (!entry.exception) {
      return;
    }

    this.expandedId.update((x) => (x === entry.id ? null : entry.id));
  }

  public levelClass(level: string): string {
    return levelClass(level);
  }

  /** The whole filtered set, against what is on screen — so a truncated view says it is truncated. */
  public readonly countLabel = computed(() => {
    if (this.isLoading()) {
      return '';
    }

    const shown = this.entries().length;
    const total = this.totalCount();

    if (total > shown) {
      return `${shown} of ${total}`;
    }

    return total === 1 ? '1 entry' : `${total} entries`;
  });

  /** Replaces the buffer with the newest page for the filter as it now stands. */
  private reload(): void {
    const id = ++this.requestId;
    this.isLoading.set(true);
    this.expandedId.set(null);

    this.logService.query(this.buildQuery()).subscribe({
      next: (page) => {
        if (id !== this.requestId) {
          return;
        }

        this.entries.set(page.entries);
        this.totalCount.set(page.totalCount);
        this.hasMore.set(page.hasMore);
        this.isLoading.set(false);
      },
      error: (error: unknown) => {
        if (id !== this.requestId) {
          return;
        }

        this.isLoading.set(false);
        this.toastr.error(apiErrorMessage(error, 'Could not read the log'));
        this.isFollowing.set(false);
      },
    });
  }

  /** Asks for whatever is newer than the newest line in hand and puts it on top. */
  private tail(): void {
    const newest = this.entries()[0]?.id ?? 0;

    if (newest === 0) {
      // Nothing in hand to be newer than — including the case where the log was empty on the first
      // load — so ask for the page rather than for a tail of nothing.
      this.reload();
      return;
    }

    const id = this.requestId;

    this.logService.query({ ...this.buildQuery(), afterId: newest }).subscribe({
      next: (page) => {
        // A filter changed while this was in flight: its answer belongs to the old one.
        if (id !== this.requestId) {
          return;
        }

        // The tally still describes the whole filtered set, so it is taken even when nothing new
        // arrived — that is how the screen notices entries it is not showing.
        this.totalCount.set(page.totalCount);

        if (page.entries.length === 0) {
          return;
        }

        this.entries.update((current) => [...page.entries, ...current].slice(0, maxBuffered));
        this.hasMore.set(this.totalCount() > this.entries().length);
      },
      error: () => {
        // Silent: a failed tail is usually a session that has just expired, and the interceptor is
        // already routing to the sign-in screen. A toast on top of that is not help.
        this.isFollowing.set(false);
      },
    });
  }

  private buildQuery(): ILogQuery {
    return {
      minLevel: this.minLevel() || undefined,
      search: this.search().trim() || undefined,
      from: toIsoUtc(this.from()),
      to: toIsoUtc(this.to()),
    };
  }
}

/**
 * A `datetime-local` value to the ISO instant the API expects.
 *
 * The input yields local wall-clock with no zone ("2026-08-31T14:00"), and the log is stored in
 * UTC — so the string has to go through `Date`, which reads it as local time, before being written
 * back as an instant. Sending it through unconverted would shift the whole range by the viewer's
 * own offset, which is the kind of bug that only shows up outside UTC.
 */
function toIsoUtc(value: string): string | undefined {
  if (!value) {
    return undefined;
  }

  const parsed = new Date(value);

  if (Number.isNaN(parsed.getTime())) {
    return undefined;
  }

  return parsed.toISOString();
}
