import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject } from '@angular/core';
import { VersionService } from '@services/version.service';

/**
 * What this installation is: which release is running, where the source and the image come from,
 * and the two licences that apply — MIT for the code, and the wordmark's own, which does not
 * travel with it.
 *
 * The build facts come from `version.json`; everything else is a constant, because it is a fact
 * about the project rather than about the running copy. They are kept here beside each other so
 * the screen is one file to correct when the README changes, which is the document they mirror.
 */
@Component({
  selector: 'app-about',
  standalone: true,
  imports: [DatePipe],
  templateUrl: 'about.component.html',
  styleUrl: 'about.component.scss',
})
export class AboutComponent implements OnInit {
  private readonly versionService = inject(VersionService);

  public readonly version = this.versionService.version;
  public readonly loaded = this.versionService.loaded;

  public readonly repositoryUrl = 'https://github.com/kalinkasolutions/FileHub';
  public readonly imageUrl = 'https://hub.docker.com/r/kalinkasolutions/filehub';
  public readonly licenseUrl = 'https://github.com/kalinkasolutions/FileHub/blob/master/LICENSE';
  public readonly typefaceUrl = 'https://brandsemut.com/product/greater-theory/';

  /**
   * A full sha is noise on screen; seven characters is enough to find the commit.
   *
   * The `?? ''` is on the *field*, not on the object: `version.json` is generated, and a release
   * build that passed a tag but no sha would otherwise be a `slice` of undefined inside a computed
   * — which is a blank screen rather than a missing row.
   */
  public readonly shortSha = computed(() => (this.version()?.commitSha ?? '').slice(0, 7));

  /** The commit this build came from, on the forge — only useful once there is a sha to point at. */
  public readonly commitUrl = computed(() => {
    const sha = this.version()?.commitSha;
    return sha ? `${this.repositoryUrl}/commit/${sha}` : '';
  });

  public ngOnInit(): void {
    void this.versionService.ensureLoaded();
  }
}
