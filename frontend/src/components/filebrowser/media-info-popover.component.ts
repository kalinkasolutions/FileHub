import { OverlayRef } from '@angular/cdk/overlay';
import {
  Component,
  InjectionToken,
  OnInit,
  afterRenderEffect,
  computed,
  inject,
  signal,
} from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { IMediaInfo, IMediaInfoTarget, mediaMetaLine, sourceName } from '@models/IMediaInfo';
import { apiErrorMessage } from '@services/api-error';
import { AuthService } from '@services/auth.service';
import { MediaInfoService } from '@services/media-info.service';
import { finalize } from 'rxjs';

/** Which folder the popover is about. Provided by whoever opens it, beside the `OverlayRef`. */
export const MEDIA_INFO_TARGET = new InjectionToken<IMediaInfoTarget>('media info target');

/**
 * The card behind a folder's info button: poster, title, year, rating, genres and a summary, from
 * TMDb or IGDB by way of the server's cache. It is a panel floated next to the button rather than a
 * dialog, because it is a glance — the listing stays where it was and a tap anywhere else closes it.
 */
@Component({
  selector: 'app-media-info-popover',
  standalone: true,
  imports: [MatIconModule],
  templateUrl: './media-info-popover.component.html',
  styleUrl: './media-info-popover.component.scss',
})
export class MediaInfoPopoverComponent implements OnInit {
  private readonly target = inject(MEDIA_INFO_TARGET);
  private readonly overlayRef = inject(OverlayRef);
  private readonly mediaInfoService = inject(MediaInfoService);
  private readonly authService = inject(AuthService);

  public readonly name = this.target.name;
  public readonly info = signal<IMediaInfo | null>(null);
  public readonly error = signal('');
  public readonly isLoading = signal(true);

  /** Only an admin may ask the source again: each refresh is a request to a third party. */
  public readonly isAdmin = this.authService.isAdmin;

  public readonly meta = computed(() => {
    const info = this.info();
    return info ? mediaMetaLine(info) : '';
  });

  public readonly source = computed(() => {
    const info = this.info();
    return info ? sourceName(info.source) : '';
  });

  public readonly posterUrl = computed(() => {
    const info = this.info();
    return info?.hasPoster ? this.mediaInfoService.posterUrl(info.id) : '';
  });

  /** The id tag a renamed folder can carry to pin the match. TMDb numbers movies and shows
   * separately, so its tag names the type as well. */
  public readonly idTagExample = computed(() =>
    this.info()?.source === 'igdb' ? '{igdb-1020}' : '{tmdb-movie-693134}',
  );

  public constructor() {
    // The overlay picks above or below when the card is attached, which is while it still says
    // "Looking it up…" and is three lines tall. The answer makes it five times that, and a card that
    // fitted below the last row no longer does — so it is measured again whenever what it shows
    // changes, after that has been drawn.
    afterRenderEffect(() => {
      this.info();
      this.error();
      this.isLoading();
      this.overlayRef.updatePosition();
    });
  }

  public ngOnInit(): void {
    // What this session already saw is shown at once; the server's cache answers the rest.
    const known = this.mediaInfoService.known(this.target);
    if (known) {
      this.info.set(known);
      this.isLoading.set(false);
      return;
    }

    this.load(this.mediaInfoService.get(this.target));
  }

  public refresh(): void {
    this.load(this.mediaInfoService.refresh(this.target));
  }

  public close(): void {
    this.overlayRef.dispose();
  }

  private load(request: ReturnType<MediaInfoService['get']>): void {
    this.isLoading.set(true);
    this.error.set('');

    request.pipe(finalize(() => this.isLoading.set(false))).subscribe({
      next: (info) => this.info.set(info),
      error: (error: unknown) => this.error.set(apiErrorMessage(error, 'Could not look this up')),
    });
  }
}
