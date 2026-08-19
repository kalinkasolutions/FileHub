/**
 * A link that was aimed at one of the caller's groups, as a member of that group sees it
 * (`ReceivedShareDto`). Only an admin can aim a link at a group, so every one of these was put
 * there deliberately by somebody who administers the install.
 *
 * Thinner than {@link IShareLink} on purpose: no base path and no relative path. A member of the
 * audience may hold no grant on the base path the file sits in, so the API does not name the
 * directories above it.
 */
export interface IReceivedShare {
  id: string;

  /** Name of the shared file or directory. */
  name: string;

  isDir: boolean;

  /** Total **bytes**, measured when the link was created — a directory is not an item count here. */
  size: number;

  downloadCount: number;

  /** 0 means unlimited. Shown so a member can see a link is nearly spent before following it. */
  maxDownloadCount: number;

  createdAt: string;

  /** Which of the caller's groups it was aimed at. Never null: that is what put it in this list. */
  audienceGroupId: string;

  audienceGroupName: string;

  /** Who shared it, by display name. */
  sharedBy: string;

  /** The absolute public URL, stamped by the API. */
  link: string;
}
