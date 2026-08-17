/** A public link as the user who created it sees it (`ShareDto`). */
export interface IShareLink {
  id: string;

  /** Name of the shared file or directory. */
  name: string;

  basePathId: string;

  /** Path below the base path, so two links to the same name can be told apart. */
  relativePath: string;

  isDir: boolean;

  /** Total **bytes**, measured when the link was created — a directory is not an item count here. */
  size: number;

  downloadCount: number;

  /** 0 means unlimited. */
  maxDownloadCount: number;

  createdAt: string;

  /**
   * Null means the link is anonymous by URL — anyone holding it can open it. Set, it only answers a
   * signed-in member of that group, so the URL on its own is worth nothing to anyone else.
   */
  audienceGroupId: string | null;

  /** The audience group's name, so a row can be labelled without a second call. Null when there is none. */
  audienceGroupName: string | null;

  /** The absolute public URL, stamped by the API. */
  link: string;
}

/** The body of `POST /api/share`. */
export interface ICreateShare {
  basePathId: string;

  /** Empty shares the base path itself. */
  relativePath: string;

  /** 0 means unlimited. */
  maxDownloadCount: number;

  /**
   * Who the link is for. Omitted or null is the default and keeps today's behaviour: anonymous by
   * URL. Set to one of the caller's groups, only a signed-in member of it can open the link.
   */
  audienceGroupId?: string | null;
}
