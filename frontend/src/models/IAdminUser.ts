/** One row of `GET /api/admin/users`. */
export interface IAdminUser {
  id: string;
  /** Display name. Not what the user signs in with — that is the email address. */
  username: string;
  email: string;
  /**
   * False means the invitation was never accepted: the account exists but has no password and
   * cannot sign in. The list shows such an account as "invited".
   */
  emailConfirmed: boolean;
  roles: string[];
  /** The password was set by somebody else, so the account has to choose its own first. */
  mustChangePassword: boolean;
  /** The account is disabled: its lockout runs into the far future. */
  isLockedOut: boolean;
  /**
   * How many base paths it has been granted <em>directly</em>. Zero does not mean it can see
   * nothing: access is the union of its own grants and those of every group it belongs to, and the
   * Admin role reaches every base path without a grant at all.
   */
  basePathCount: number;
  createdAt: string;
}

/**
 * `POST /api/admin/users`. There is no password field on purpose — FileHub has no registration
 * page, and the invitation mail is what sets the first password.
 */
export interface IInviteUser {
  username: string;
  email: string;
  roles: string[];
}

/**
 * The answer to an invitation. The two fields are independent: the account exists as soon as
 * `userId` is set, even when the mail did not go out, so a false `inviteMailSent` is something to
 * tell the admin about rather than an error to report as a failed invite.
 */
export interface IInviteResult {
  userId: string;
  inviteMailSent: boolean;
  /** Why it did not go out, empty when it did. Usually the SMTP error verbatim. */
  inviteMailError: string;
}

/**
 * `PUT /api/admin/users/{id}`. `email` is carried so the whole user round-trips, but it must match
 * the address the account already has — the API refuses an address change here, because a new one
 * has to be confirmed from the user's own account screen.
 */
export interface IUpdateUser {
  username: string;
  email: string;
  roles: string[];
}

/** `PUT /api/admin/users/{id}/lockout`. */
export interface ISetLockout {
  locked: boolean;
}

export type UserStatus = 'active' | 'invited' | 'disabled';

/**
 * Disabled wins over invited: an account that is both cannot sign in for the more urgent of the
 * two reasons, and re-enabling it is what has to happen first.
 */
export function userStatus(user: IAdminUser): UserStatus {
  if (user.isLockedOut) {
    return 'disabled';
  }

  if (!user.emailConfirmed) {
    return 'invited';
  }

  return 'active';
}

export const userStatusLabel: Record<UserStatus, string> = {
  active: 'Active',
  invited: 'Invited',
  disabled: 'Disabled',
};

/**
 * What this account can reach, in one line. The row only knows the *direct* grants, so it must not
 * claim more than that: an admin sees everything whatever the count says, and a user with no direct
 * grant may still reach base paths through a group the row cannot see from here.
 */
export function accessLabel(user: IAdminUser, adminRole: string): string {
  if (user.roles.includes(adminRole)) {
    return 'Admin — sees every base path';
  }

  if (user.basePathCount === 0) {
    return 'No base path granted directly — it sees only what its groups grant';
  }

  if (user.basePathCount === 1) {
    return 'Granted 1 base path directly, plus whatever its groups grant';
  }

  return `Granted ${user.basePathCount} base paths directly, plus whatever its groups grant`;
}

/** Adds or removes one role, keeping the array a new one so a signal sees the change. */
export function toggleRole(roles: readonly string[], role: string, on: boolean): string[] {
  if (on) {
    return roles.includes(role) ? [...roles] : [...roles, role];
  }

  return roles.filter((r) => r !== role);
}

/** Newest last is how they were created; the list reads better with the newest account on top. */
export function sortUsers(users: readonly IAdminUser[]): IAdminUser[] {
  return [...users].sort((a, b) => b.createdAt.localeCompare(a.createdAt));
}
