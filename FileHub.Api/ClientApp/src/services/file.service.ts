import { Injectable } from '@angular/core';
import { IFileEntry } from '@models/IFileEntry';

/**
 * Encodes a relative path for the download route's catch-all segment.
 *
 * Each segment is encoded **separately** and the separators are put back verbatim: encoding the
 * whole path in one go turns every `/` into `%2F`, which Kestrel rejects before routing ever sees
 * it, while leaving it unencoded would let a `#` or a `?` in a file name truncate the URL. Splitting
 * on `/` is right because the server builds `nextSegment` with the host separator and the
 * deployment target is Linux.
 */
export function encodeRelativePath(relativePath: string): string {
  return relativePath.split('/').map(encodeURIComponent).join('/');
}

/**
 * The name the browser should save under. A directory arrives as a zip built on the fly, and the
 * `download` attribute overrides the server's Content-Disposition, so the extension has to be added
 * here or the file lands without one.
 */
export function downloadName(name: string, isDir: boolean): string {
  return isDir ? `${name}.zip` : name;
}

/** Starting a download. Nothing here goes through `HttpClient`: the response is a stream the browser
 * has to own — buffering a multi-gigabyte file into memory to hand it back would defeat the point. */
@Injectable({ providedIn: 'root' })
export class FileService {
  /** `GET /api/files/download/{basePathId}/{*relativePath}`; a directory streams as a zip. */
  public downloadUrl(basePathId: string, relativePath: string): string {
    return `/api/files/download/${encodeURIComponent(basePathId)}/${encodeRelativePath(relativePath)}`;
  }

  public download(entry: IFileEntry): void {
    this.start(
      this.downloadUrl(entry.id, entry.nextSegment),
      downloadName(entry.name, entry.isDir),
    );
  }

  /**
   * A synthetic anchor rather than assigning `location.href`: with the `download` attribute a failed
   * request is simply dropped, where a navigation would replace the app with the error body.
   */
  private start(url: string, name: string): void {
    const link = document.createElement('a');
    link.href = url;
    link.download = name;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  }
}
