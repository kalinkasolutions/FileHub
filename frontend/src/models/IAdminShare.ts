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
  /**
   * Null means the link answers anyone who has it. Set, it only answers a signed-in member of that
   * group (and admins) — every other caller gets the same refusal an unknown id gets.
   */
  audienceGroupId: string | null;
  /** Name of that group; empty when the link is anonymous. */
  audienceGroupName: string;
  /** The absolute public URL, stamped on by the API from its configured base address. */
  link: string;
}

/**
 * Who the link answers. This is the whole difference between a URL that is readable by the internet
 * and one that is not, so the list says it on every row rather than only on the restricted ones.
 */
export function audienceLabel(share: IAdminShare): string {
  if (!share.audienceGroupId) {
    return 'Anyone with the link';
  }

  return `Only ${share.audienceGroupName}`;
}

/** A link aimed at a group needs a sign-in, so it is not reachable from the internet by itself. */
export function isRestricted(share: IAdminShare): boolean {
  return share.audienceGroupId !== null && share.audienceGroupId !== undefined;
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
