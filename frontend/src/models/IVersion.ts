/**
 * Contents of `wwwroot/version.json`, written into the image by the release build — see the last
 * stage of the `Dockerfile` and `.github/workflows/docker-build-and-push.yml`.
 *
 * It is written *after* the SPA is copied in, so it is not part of the Angular build output and
 * never appears in a local `npm run build`. A development build therefore has no version at all,
 * which is a state the about screen has to draw rather than an error to report.
 */
export interface IVersion {
  /** The GitHub release tag the image was built from, e.g. `v1.4.0`. Empty on a plain docker build. */
  version: string;
  /** Full commit sha of that release; empty when the build did not pass one. */
  commitSha: string;
  /** ISO-8601 build time; empty when the build did not pass one. */
  builtAt: string;
}
