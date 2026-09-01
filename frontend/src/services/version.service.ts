import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable, Signal, inject, signal } from '@angular/core';
import { skipLoadingOverlay } from '@interceptors/loading.interceptor';
import { IVersion } from '@models/IVersion';
import { firstValueFrom } from 'rxjs';

/**
 * Which release is running. `version.json` is baked into the image by the release build, so it
 * cannot change while the app runs: one fetch, shared by every caller, cached in a signal.
 *
 * A missing or unreadable file means "development build" and not a failure — a local build has no
 * version.json at all, and the request 404s, which is why the whole load is wrapped in a catch that
 * says nothing. (It 404s rather than being answered with index.html because the SPA fallback is
 * routed on `{*path:nonfile}`, and a path whose last segment has an extension is not matched.)
 */
@Injectable({ providedIn: 'root' })
export class VersionService {
  private readonly http = inject(HttpClient);

  private readonly state = signal<IVersion | null>(null);
  private readonly settled = signal(false);

  /** The running release, or null for a build that carries no version. */
  public readonly version: Signal<IVersion | null> = this.state.asReadonly();

  /** True once the lookup finished, so a caller can tell "still asking" from "no release info". */
  public readonly loaded: Signal<boolean> = this.settled.asReadonly();

  private loading?: Promise<void>;

  /** Loads the version once; concurrent callers share the one in-flight request. */
  public ensureLoaded(): Promise<void> {
    this.loading ??= this.load();
    return this.loading;
  }

  private async load(): Promise<void> {
    try {
      const version = await firstValueFrom(
        this.http.get<IVersion>('/version.json', {
          // Served with no explicit freshness, so a browser may heuristically hold the previous
          // release's copy; no-cache forces the revalidation that gets the new one.
          headers: { 'Cache-Control': 'no-cache' },
          // Nobody asked for this request — it happens because a screen was opened. The overlay is
          // for something a person is waiting on.
          context: new HttpContext().set(skipLoadingOverlay, true),
        }),
      );

      // A plain `docker build` leaves the build args empty, which is the same as having no release.
      this.state.set(version.version ? version : null);
    } catch {
      // No version.json, or the fallback's index.html instead of one. A local build; nothing to say.
    } finally {
      this.settled.set(true);
    }
  }
}
