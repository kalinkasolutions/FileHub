/**
 * Shape of `GET /api/auth/status`. It answers for anonymous callers too — `authenticated: false`
 * with the rest empty — so it is the one call the app can always make.
 *
 * The wire is camelCase: ASP.NET Core serialises with the camelCase policy by default and binds
 * incoming JSON case-insensitively, so the C# `Authenticated` arrives here as `authenticated`.
 */
export interface IAuthStatus {
  authenticated: boolean;
  userId: string | null;
  username: string | null;
  email: string | null;
  /** Role names, e.g. `['Admin']`. Empty for a plain user. */
  roles: string[];
  /**
   * The account was given its password by someone else (an invite, or an admin reset) and has to
   * choose its own before it can go anywhere. `passwordChangeGuard` is what enforces it.
   */
  mustChangePassword: boolean;
}
