import { Injectable, computed, signal } from '@angular/core';
import { IFileEntry } from '@models/IFileEntry';
import { IPublicPath } from '@models/IPublicPath';
import { Observable, of } from 'rxjs';

/** One crumb of the trail: a directory the user has opened. */
export interface IPathSegment {
  /** The base path it lives under. */
  basePathId: string;

  /** What the breadcrumb shows. */
  name: string;

  /** Path below the base path, without a leading separator; empty is the base path itself. */
  path: string;
}

/** What this service keeps in `history.state`, alongside whatever the router put there. */
interface IPathState {
  pathSegments?: IPathSegment[];
}

/**
 * Where the user is in the tree, and the browser's back button.
 *
 * The trail is mirrored into `history.pushState` on every step down and read back on `popstate`, so
 * back and forward walk the directory tree even though the URL never changes — the app is one route.
 * An empty trail is the top: the listing of the caller's base paths.
 *
 * The state object is *merged* into the existing one rather than replacing it. The router keeps its
 * `navigationId` there, and overwriting it (which the pre-rewrite service did) leaves entries the
 * router cannot restore.
 */
@Injectable({ providedIn: 'root' })
export class PathService {
  private readonly trail = signal<IPathSegment[]>([]);

  public readonly segments = this.trail.asReadonly();

  /** The directory being listed, or null at the top, where the listing is the base paths. */
  public readonly current = computed<IPathSegment | null>(() => this.trail().at(-1) ?? null);

  public readonly isAtTop = computed(() => this.trail().length === 0);

  public constructor() {
    // A reload lands on the same history entry, which still carries the trail that was pushed for
    // it — so restore rather than reset, and only write a trail in when there is none.
    const restored = this.readState();

    if (restored) {
      this.trail.set(restored);
    } else {
      this.writeState([], false);
    }

    window.addEventListener('popstate', () => this.trail.set(this.readState() ?? []));
  }

  /** Steps into a directory. Re-opening one that is already on the trail is a no-op, so a listing
   * that shows a directory twice cannot grow the breadcrumb twice. */
  public open(entry: IFileEntry): void {
    const segment: IPathSegment = {
      basePathId: entry.id,
      name: entry.name,
      path: entry.nextSegment,
    };

    const trail = this.trail();

    // Matched on (base path, path) rather than on `itemId`: the server mints a fresh `itemId` for
    // every listing, so the same directory never carries the same one twice.
    const known = trail.some((x) => x.basePathId === segment.basePathId && x.path === segment.path);

    if (known) {
      return;
    }

    this.push([...trail, segment]);
  }

  /** Jumps back to the crumb at `index`, dropping everything below it. */
  public goTo(index: number): void {
    const trail = this.trail();

    if (index < 0 || index >= trail.length - 1) {
      return;
    }

    this.push(trail.slice(0, index + 1));
  }

  /** One level up. */
  public up(): void {
    const trail = this.trail();

    if (trail.length === 0) {
      return;
    }

    this.push(trail.slice(0, -1));
  }

  /** All the way back to the base-path listing. */
  public goHome(): void {
    if (this.trail().length === 0) {
      return;
    }

    this.push([]);
  }

  private push(segments: IPathSegment[]): void {
    this.trail.set(segments);
    this.writeState(segments, true);
  }

  private readState(): IPathSegment[] | null {
    const state = history.state as IPathState | null;
    return state?.pathSegments ?? null;
  }

  private writeState(segments: IPathSegment[], push: boolean): void {
    const state = { ...history.state, pathSegments: segments };

    if (push) {
      history.pushState(state, '');
      return;
    }

    history.replaceState(state, '');
  }

  // ─── For the screens that are still pre-rewrite ───────────────────────────

  /**
   * @deprecated The breadcrumb the pre-rewrite header renders. The browser draws its own now, so
   * this stays empty rather than showing it twice; it exists only so that header goes on compiling.
   */
  public readonly NextSegment$: Observable<IPublicPath[]> = of([]);

  /** @deprecated The pre-rewrite header's breadcrumb click. Use {@link goTo}. */
  public segmentChange(_segment: IPublicPath): void {
    // Nothing: the header's breadcrumb is empty, so this is never reached.
  }

  /** @deprecated The admin share list uses this to name a share from its stored path. `ShareDto`
   * and `AdminShareDto` both carry `name` now, so it has nothing left to do. */
  public static getPathName(path: string): string {
    return path.substring(path.lastIndexOf('/') + 1);
  }
}
