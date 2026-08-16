import { HttpClient } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { ActivatedRoute, Router } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { firstValueFrom } from 'rxjs';
import { formatSize } from './formatsize';

/** Shape of `GET /public-api/share/{id}` — everything an anonymous caller is told about a link. */
export interface IPublicShare {
  id: string;
  name: string;
  /** Total bytes, read from the share row: the public routes never walk the tree. */
  size: number;
  isDir: boolean;
}

/**
 * The share landing page — the only screen an anonymous visitor sees. It assumes no session: it
 * calls nothing but `public-api`, and the shell renders the header in its signed-out form for it,
 * so a visitor who happens to be signed in sees exactly what the stranger the link was sent to
 * does.
 *
 * A link that never existed, has expired or has been used up is not an error to report but a dead
 * end to explain, so both shapes the API answers with — a 404 and a redirect to `/404` — land on
 * the same screen.
 */
@Component({
  standalone: true,
  selector: 'public-share',
  templateUrl: './publicshare.component.html',
  styleUrl: './publicshare.component.scss',
  imports: [MatIconModule],
})
export class PublicShareComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly toastr = inject(ToastrService);

  public readonly share = signal<IPublicShare | null>(null);
  public readonly loading = signal(true);

  public async ngOnInit(): Promise<void> {
    const id = this.route.snapshot.paramMap.get('id') ?? '';

    try {
      this.share.set(await firstValueFrom(this.http.get<IPublicShare>(`/public-api/share/${id}`)));
    } catch {
      // No toast: a dead link is this page's subject, not an incident. `/404` is also where the
      // download route sends a browser, so the two dead ends look the same.
      void this.router.navigate(['/404']);
    } finally {
      this.loading.set(false);
    }
  }

  public readonly formatSize = formatSize;

  /**
   * A full URL rather than a relative one: this is the address that gets pasted into a chat, and
   * the browser is sent straight to it so a directory's zip streams out of the API instead of
   * through the app.
   */
  public downloadUrl(): string {
    return `${location.origin}/public-api/share/${this.share()?.id}/download`;
  }

  public async copyLink(): Promise<void> {
    try {
      await navigator.clipboard.writeText(location.href);
      this.toastr.success('Link copied');
    } catch {
      // Clipboard access is denied on any non-secure origin; the address bar is the fallback.
      this.toastr.error('Could not copy — copy the address from your browser instead');
    }
  }
}
