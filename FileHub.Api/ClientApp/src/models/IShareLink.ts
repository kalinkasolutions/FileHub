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
}
