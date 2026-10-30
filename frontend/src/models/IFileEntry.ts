/**
 * One row of a listing — a base path, a directory or a file (`FileEntryDto`).
 *
 * Note what `id` is *not*: it identifies the **base path** the entry lives under, never the entry
 * itself. A row is addressed by the pair (`id`, `nextSegment`), which is what navigating,
 * downloading and sharing all send back.
 */
export interface IFileEntry {
  /** The base path this entry lives under. A Guid string — it was an int before the .NET rewrite. */
  id: string;

  name: string;

  isDir: boolean;

  /** Bytes for a file; the **number of entries it holds** for a directory. */
  size: number;

  /** Path below the base path, **without a leading separator**. Empty for a base path itself. */
  nextSegment: string;

  isBasePath: boolean;

  /**
   * A folder directly under a base path marked media or games: it gets the info button, and
   * `api/media/info` answers for it. The server decides this; the client only draws it.
   */
  hasInfo: boolean;

  /**
   * A fresh id per listing. It is regenerated on every response, so it is good for identity
   * comparisons *within* one in-memory listing and for nothing else — never persist it, never
   * compare it against an id from another request.
   */
  itemId: string;
}
