import { Injectable, inject } from '@angular/core';
import { SwUpdate } from '@angular/service-worker';
import { ToastrService } from 'ngx-toastr';
import { filter, interval } from 'rxjs';

/**
 * How often a copy that stays open asks whether a new image has been deployed. Six hours: the thing
 * being watched for is an operator pulling a new image, which happens on the scale of days, and the
 * check costs one conditional request for `ngsw.json`.
 */
const checkInterval = 6 * 60 * 60 * 1000;

/**
 * Tells the user when the service worker has a newer build ready, and reloads onto it when they
 * say so.
 *
 * Without this an installed copy runs whatever bundle it cached until every tab of it is closed,
 * which on a phone's home screen can be weeks. FileHub is deployed by pulling a new image, so the
 * stale bundle would be talking to an API that has already moved — and the person seeing the
 * breakage has no way to know a reload is the fix.
 *
 * It is a toast the user dismisses or acts on, not an automatic reload: a reload in the middle of
 * filling in the mail settings, or half way through a folder, loses what they were doing. The new
 * build is already downloaded and will be used on the next cold start regardless.
 */
@Injectable({ providedIn: 'root' })
export class AppUpdateService {
  private readonly updates = inject(SwUpdate);
  private readonly toastr = inject(ToastrService);

  public start(): void {
    // False whenever the worker is not running at all — a development build, or a browser that
    // refused to register it. Subscribing anyway would never emit, but this says why.
    if (!this.updates.isEnabled) {
      return;
    }

    this.updates.versionUpdates
      .pipe(filter((event) => event.type === 'VERSION_READY'))
      .subscribe(() => this.announce());

    // The worker only asks the server for a new version when it starts, which is once per page
    // load — so without a poll of our own the notice above fires on the next cold start and never
    // during the long-lived session it was written for. An installed copy is resumed rather than
    // reloaded, which is the whole point of installing it.
    //
    // No stabilisation guard is needed: the first tick is six hours away, so it cannot compete with
    // the requests the screen was opened to make.
    interval(checkInterval).subscribe(() => {
      // A check that fails is not worth a word to anybody — the next tick tries again, and the
      // whole point of this is that nobody is waiting on it.
      void this.updates.checkForUpdate().catch(() => undefined);
    });
  }

  private announce(): void {
    this.toastr
      .info('Tap to reload.', 'A new version of FileHub is ready', {
        disableTimeOut: true,
        tapToDismiss: false,
        closeButton: true,
      })
      .onTap.subscribe(() => void this.reload());
  }

  /**
   * `activateUpdate` before the reload rather than after it: it makes the downloaded version the
   * one this client is served, so the page that comes back is certainly the new one rather than
   * whatever the worker happens to consider current by then. A failure is not worth reporting —
   * the reload is what the user asked for, and it happens either way.
   */
  private async reload(): Promise<void> {
    await this.updates.activateUpdate().catch(() => undefined);
    document.location.reload();
  }
}
