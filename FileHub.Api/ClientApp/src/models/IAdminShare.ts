/**
 * A public link as an admin sees it: the share plus who made it and which base path it points
 * into, since the admin list spans every user's links. `GET /api/admin/shares`.
 */
export interface IAdminShare {
  id: string;
  name: string;
  basePathId: string;
  basePathName: string;
  /** Where inside the base path the link points. Empty when it is the base path itself. */
  relativePath: string;
  isDir: boolean;
  size: number;
  downloadCount: number;
  /** `0` means unlimited — nothing in the API sets a limit yet. */
  maxDownloadCount: number;
  createdAt: string;
  createdById: string;
  createdBy: string;
  /** The absolute public URL, stamped on by the API from its configured base address. */
  link: string;
}

/** Base path plus relative path, the way the row shows where a link actually points. */
export function shareLocation(share: IAdminShare): string {
  if (!share.relativePath) {
    return share.basePathName;
  }

  return `${share.basePathName}/${share.relativePath}`;
}

/**
 * `n` downloads, or `n of max` for a limited link. The limit is enforced by the API, so a link
 * that reached it is dead — worth showing rather than hiding behind a bare count.
 */
export function downloadsLabel(share: IAdminShare): string {
  if (share.maxDownloadCount > 0) {
    return `${share.downloadCount} of ${share.maxDownloadCount} downloads`;
  }

  return share.downloadCount === 1 ? '1 download' : `${share.downloadCount} downloads`;
}
