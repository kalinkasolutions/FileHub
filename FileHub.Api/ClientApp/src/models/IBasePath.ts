/**
 * A configured base path: an absolute directory on the host FileHub is allowed to read.
 * Admin-only — a browsing user never sees one of these, only the entries inside it.
 */
export interface IBasePath {
  id: string;
  /** Absolute path on the host, as the API normalised it (no trailing separator). */
  path: string;
  /** Label shown in the UI; the directory name when the admin left it empty. */
  name: string;
  createdAt: string;
  /**
   * How many users have been granted it. `0` is a normal state and means *nobody* can see it —
   * admins included, since the admin role does not imply a grant.
   */
  userCount: number;
}

/** Body of `POST /api/admin/base-path` and `PUT /api/admin/base-path/{id}`. */
export interface ISaveBasePath {
  /** Has to be absolute and has to exist on the host — the API checks both and says which failed. */
  path: string;
  /** Optional; the API falls back to the directory name when it is empty. */
  name: string;
}
