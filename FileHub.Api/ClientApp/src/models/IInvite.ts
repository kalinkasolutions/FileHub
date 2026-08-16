/**
 * Payload behind the link in an invitation mail. There is no registration in FileHub — an account
 * exists because an admin invited it, and this call is what gives it its first password and
 * confirms the address.
 */
export interface IAcceptInvite {
  userId: string;
  token: string;
  password: string;
  /** Optional display name; the server keeps the one it was invited with if this is empty. */
  username?: string;
}

/** Payload behind the link mailed to a user's new address when they change their email. */
export interface IConfirmEmailChange {
  userId: string;
  /** The new address the token was issued for. */
  email: string;
  token: string;
}
