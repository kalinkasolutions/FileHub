/**
 * A group: a named set of accounts that base paths can be granted to. One row of
 * `GET /api/admin/groups`.
 *
 * A user's access to a base path is the union of their own grants and the grants of every group
 * they belong to, so a group is a second, independent route to a directory — which is why the two
 * counts below are both worth showing: a group only does something when it has both halves.
 */
export interface IGroup {
  id: string;
  /** Unique and compared case-insensitively by the API, which answers 400 on a duplicate. */
  name: string;
  /** How many accounts belong to it. */
  memberCount: number;
  /** How many base paths it grants its members. */
  basePathCount: number;
  createdAt: string;
}

/** Body of `POST /api/admin/groups` and `PUT /api/admin/groups/{id}` — a group is only its name. */
export interface ISaveGroup {
  name: string;
}

export function memberCountLabel(group: IGroup): string {
  return group.memberCount === 1 ? '1 member' : `${group.memberCount} members`;
}

export function basePathCountLabel(group: IGroup): string {
  return group.basePathCount === 1 ? '1 base path' : `${group.basePathCount} base paths`;
}

/**
 * Why a group is not doing anything, or empty when it is. Both halves are needed for a group to
 * grant anything at all, and neither half is an error — a group is routinely created before either
 * is filled in — so this is a line of explanation rather than a failure.
 */
export function groupWarning(group: IGroup): string {
  if (group.memberCount === 0 && group.basePathCount === 0) {
    return 'Nobody is in it and it grants nothing yet.';
  }

  if (group.memberCount === 0) {
    return 'Nobody is in it, so the base paths it grants reach no one.';
  }

  if (group.basePathCount === 0) {
    return 'It grants no base paths, so being a member gets you nothing.';
  }

  return '';
}

/**
 * What leaving this group — or losing it altogether — costs an account. A group that grants nothing
 * costs nothing today, and a confirmation that says "they lose the 0 base path(s) it grants"
 * instead is one nobody reads the next time.
 */
export function groupLossLabel(group: IGroup): string {
  if (group.basePathCount === 0) {
    return (
      'It grants no base paths, so nothing is lost today — but they will not get whatever it is ' +
      'granted later.'
    );
  }

  return (
    `They lose the ${basePathCountLabel(group)} it grants, unless they reach them another way, ` +
    `and every share link they made under them is deleted.`
  );
}

/** Newest first, the way the accounts and the links are listed. */
export function sortGroups(groups: readonly IGroup[]): IGroup[] {
  return [...groups].sort((a, b) => b.createdAt.localeCompare(a.createdAt));
}
