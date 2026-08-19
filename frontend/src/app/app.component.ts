import { AsyncPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { HeaderComponent } from '@components/header/header.component';
import { LoadingService } from '@services/loading.service';
import { filter, map } from 'rxjs';

/** What a route asks of the app chrome through its `data.chrome`. */
export type Chrome = 'full' | 'anonymous' | 'none';

@Component({
  standalone: true,
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss',
  imports: [RouterOutlet, HeaderComponent, AsyncPipe],
})
export class AppComponent {
  private readonly loadingService = inject(LoadingService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  public readonly isLoading = this.loadingService.loading$;

  /**
   * How much chrome the active route wants. `none` is the auth screens: they are centred forms that
   * carry their own FileHub mark, so a header above them would be a second one. `anonymous` is the
   * public share page, which is shown to whoever was sent the link and must not offer a signed-in
   * visitor anything the anonymous one doesn't get.
   */
  public readonly chrome = signal<Chrome>('full');

  public readonly showHeader = computed(() => this.chrome() !== 'none');
  public readonly anonymous = computed(() => this.chrome() === 'anonymous');

  constructor() {
    this.router.events
      .pipe(
        filter((event) => event instanceof NavigationEnd),
        map(() => this.deepestRoute().snapshot.data['chrome'] as Chrome | undefined),
        takeUntilDestroyed(),
      )
      .subscribe((chrome) => this.chrome.set(chrome ?? 'full'));
  }

  /** `data` is only inherited downwards, so the value has to be read off the leaf route. */
  private deepestRoute(): ActivatedRoute {
    let route = this.route;
    while (route.firstChild) {
      route = route.firstChild;
    }
    return route;
  }
}
