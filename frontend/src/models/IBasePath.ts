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
  /** How many users have been granted it <em>directly</em>. */
  userCount: number;
  /** How many groups have been granted it. Every member of one of those reaches it too. */
  groupCount: number;
}

/** Body of `POST /api/admin/base-path` and `PUT /api/admin/base-path/{id}`. */
export interface ISaveBasePath {
  /** Has to be absolute and has to exist on the host — the API checks both and says which failed. */
  path: string;
  /** Optional; the API falls back to the directory name when it is empty. */
  name: string;
}

/**
 * Nobody has been granted it by either route. Not an error — a base path is always created in this
 * state — but it is not invisible either: the Admin role is an implicit grant of every base path,
 * so an admin still browses it while nobody else can.
 */
export function isUngranted(basePath: IBasePath): boolean {
  return basePath.userCount === 0 && basePath.groupCount === 0;
}

/**
 * Who may see it, in one line. Access is the union of the two grant tables plus the admin role, so
 * both counts are shown rather than added together — a user granted it directly *and* through a
 * group would otherwise be counted twice.
 */
export function grantLabel(basePath: IBasePath): string {
  if (isUngranted(basePath)) {
    return 'Granted to nobody — only admins can see it';
  }

  const users = basePath.userCount === 1 ? '1 user' : `${basePath.userCount} users`;
  const groups = basePath.groupCount === 1 ? '1 group' : `${basePath.groupCount} groups`;

  return `Granted to ${users} and ${groups}, plus every admin`;
}
